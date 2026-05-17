using Commerce.Application.Exceptions;
using Commerce.Application.Models;
using Commerce.Application.Services.Admin;
using Commerce.Application.Services.Payments;
using Commerce.Tests.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Commerce.Tests.IntegrationTests.Services;

public class AdminServiceTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private AdminService _adminService = null!;
    private IStripeService _stripeMock = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        _stripeMock = Substitute.For<IStripeService>();

        _adminService = new AdminService(
            dbContext:     DbContext,
            stripeService: _stripeMock,
            logger:        Substitute.For<ILogger<AdminService>>());
    }

    // ── Arrange helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Seeds a minimal PLACED order directly in the DB — bypasses CheckoutAsync
    /// since AdminService tests focus on status transitions, not checkout logic.
    /// </summary>
    private async Task<(Order Order, Product Product)> SeedPlacedOrderAsync(
        int stock = 10, decimal price = 50m)
    {
        var user = User.Create("Admin Test User", $"{Guid.NewGuid()}@example.com",
            "Password1", phone: null);
        await SaveAsync(user);

        var product = Product.Create("Product", "Desc", price, stock, Category.Electronics);
        await SaveAsync(product);

        var snapshot = new AddressSnapshotBuilder().Build();
        var order    = Order.Create(user.Id, $"Order #{Random.Shared.Next(1000000):D9}", snapshot);
        var item     = OrderItem.Create(order.Id, product.Id, quantity: 2, unitPrice: price);
        order.AddItem(item);
        order.SetTotalAmount(price * 2);

        await SaveAsync(order); // ← only the new entity; product is already in the DB

        // Decrement stock directly — avoids re-adding the already-persisted product
        await DbContext.Products
            .Where(p => p.Id == product.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, stock - 2));

        return (order, product);
    }
    // private async Task<(Order Order, Product Product)> SeedPlacedOrderAsync(
    //     int stock = 10, decimal price = 50m)
    // {
    //     var user = User.Create("Admin Test User", $"{Guid.NewGuid()}@example.com",
    //         "Password1", phone: null);
    //     await SaveAsync(user);
    //
    //     var product = Product.Create("Product", "Desc", price, stock, Category.Electronics);
    //     await SaveAsync(product);
    //
    //     var snapshot = new AddressSnapshotBuilder().Build();
    //     var order    = Order.Create(user.Id, $"Order #{Random.Shared.Next(1000000):D9}", snapshot);
    //     var item     = OrderItem.Create(order.Id, product.Id, quantity: 2, unitPrice: price);
    //     order.AddItem(item);
    //     order.SetTotalAmount(price * 2);
    //
    //     product.DecreaseStock(2); // simulate stock reservation
    //     await SaveAsync(order, product);
    //     return (order, product);
    // }

    private async Task AttachPaymentAsync(
        Guid orderId, PaymentStatus status, string providerId = "pi_test_abc")
    {
        var payment = Payment.Create(orderId, providerId, 100m, "card");
        if (status == PaymentStatus.Completed) payment.MarkCompleted();
        if (status == PaymentStatus.Refunded)  payment.MarkRefunded();
        if (status == PaymentStatus.Failed)    payment.MarkFailed();
        await SaveAsync(payment);
    }

    // ── GetAllOrdersAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllOrders_ShouldReturnOrdersForAllUsers()
    {
        var (order1, _) = await SeedPlacedOrderAsync();
        var (order2, _) = await SeedPlacedOrderAsync();

        var (orders, total) = await _adminService.GetAllOrdersAsync(page: 1, pageSize: 20);

        total.ShouldBe(2);
        orders.Select(o => o.Id).ShouldContain(order1.Id);
        orders.Select(o => o.Id).ShouldContain(order2.Id);
    }

    [Fact]
    public async Task GetAllOrders_ShouldRespectPagination()
    {
        await SeedPlacedOrderAsync();
        await SeedPlacedOrderAsync();
        await SeedPlacedOrderAsync();

        var (orders, total) = await _adminService.GetAllOrdersAsync(page: 1, pageSize: 2);

        total.ShouldBe(3);
        orders.Count().ShouldBe(2);
    }

    // ── UpdateOrderStatusAsync ────────────────────────────────────────────────

    [Fact]
    public async Task UpdateStatus_PlacedToPaid_ShouldSucceed()
    {
        var (order, _) = await SeedPlacedOrderAsync();

        var updated = await _adminService.UpdateOrderStatusAsync(order.Id, OrderStatus.Paid);

        updated.Status.ShouldBe(OrderStatus.Paid);

        var persisted = await DbContext.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id);
        persisted.Status.ShouldBe(OrderStatus.Paid);
    }

    [Fact]
    public async Task UpdateStatus_PaidToShipped_ShouldSucceed()
    {
        var (order, _) = await SeedPlacedOrderAsync();
        order.MarkAsPaid();
        DbContext.Update(order); // ← re-attach so EF tracks the mutation
        await DbContext.SaveChangesAsync();
        foreach (var e in DbContext.ChangeTracker.Entries().ToList())
            e.State = Microsoft.EntityFrameworkCore.EntityState.Detached;

        var updated = await _adminService.UpdateOrderStatusAsync(order.Id, OrderStatus.Shipped);

        updated.Status.ShouldBe(OrderStatus.Shipped);
    }

    [Fact]
    public async Task UpdateStatus_ShippedToDelivered_ShouldSucceed()
    {
        var (order, _) = await SeedPlacedOrderAsync();
        order.MarkAsPaid();
        order.MarkAsShipped();
        DbContext.Update(order);
        await DbContext.SaveChangesAsync();
        foreach (var e in DbContext.ChangeTracker.Entries().ToList())
            e.State = Microsoft.EntityFrameworkCore.EntityState.Detached;

        var updated = await _adminService.UpdateOrderStatusAsync(order.Id, OrderStatus.Delivered);

        updated.Status.ShouldBe(OrderStatus.Delivered);
    }

    [Fact]
    public async Task UpdateStatus_CancelFromPlaced_ShouldRestoreStock()
    {
        var (order, product) = await SeedPlacedOrderAsync(stock: 10);
        // After SeedPlacedOrderAsync, stock is 8 (10 - 2 items)

        await _adminService.UpdateOrderStatusAsync(order.Id, OrderStatus.Cancelled);

        var updatedProduct = await DbContext.Products.AsNoTracking()
            .SingleAsync(p => p.Id == product.Id);
        updatedProduct.StockQuantity.ShouldBe(10); // 8 + 2 restored
    }

    [Fact]
    public async Task UpdateStatus_CancelWithCompletedPayment_ShouldInitiateRefund()
    {
        var (order, _) = await SeedPlacedOrderAsync();
        order.MarkAsPaid();
        DbContext.Update(order);
        await DbContext.SaveChangesAsync();
        foreach (var e in DbContext.ChangeTracker.Entries().ToList())
            e.State = Microsoft.EntityFrameworkCore.EntityState.Detached;

        await AttachPaymentAsync(order.Id, PaymentStatus.Completed, "pi_test_refund");

        await _adminService.UpdateOrderStatusAsync(order.Id, OrderStatus.Cancelled);

        await _stripeMock.Received(1)
            .RefundAsync("pi_test_refund", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateStatus_CancelWithPendingPayment_ShouldNotRefund()
    {
        var (order, _) = await SeedPlacedOrderAsync();
        await AttachPaymentAsync(order.Id, PaymentStatus.Pending);

        await _adminService.UpdateOrderStatusAsync(order.Id, OrderStatus.Cancelled);

        await _stripeMock.DidNotReceive()
            .RefundAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateStatus_InvalidTransition_ShouldThrowConflictException()
    {
        // PLACED → DELIVERED skips required steps
        var (order, _) = await SeedPlacedOrderAsync();

        var act = () => _adminService.UpdateOrderStatusAsync(order.Id, OrderStatus.Delivered);

        await act.ShouldThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task UpdateStatus_WhenOrderNotFound_ShouldThrowNotFoundException()
    {
        var act = () => _adminService.UpdateOrderStatusAsync(Guid.NewGuid(), OrderStatus.Paid);

        await act.ShouldThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateStatus_WhenAttemptingToSetPlaced_ShouldThrowValidationException()
    {
        var (order, _) = await SeedPlacedOrderAsync();

        // PLACED cannot be set manually — it is the initial state only
        var act = () => _adminService.UpdateOrderStatusAsync(order.Id, OrderStatus.Placed);

        await act.ShouldThrowAsync<ValidationException>();
    }
}

/// <summary>Tiny builder to avoid repeating address snapshot setup in tests.</summary>
file class AddressSnapshotBuilder
{
    public AddressSnapshot Build() => AddressSnapshot.From(
        Address.Create(Guid.NewGuid(), "Test User", "01012345678",
            "Egypt", "Cairo", "Nasr City", "Street 9",
            "12", "3", "7", "Home", isDefault: true));
}