// Commerce.Tests/IntegrationTests/Services/PaymentTimeoutJobTests.cs

using Commerce.Application.Jobs;
using Commerce.Application.Models;
using Commerce.Application.Services.Payments;
using Commerce.Tests.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Commerce.Tests.IntegrationTests.Jobs;

[Collection("Database")]
public class PaymentTimeoutJobTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private IStripeService _stripeMock = null!;
    private PaymentTimeoutJob _job = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        _stripeMock = Substitute.For<IStripeService>();
        _job = new PaymentTimeoutJob(
            DbContext,
            _stripeMock,
            Substitute.For<ILogger<PaymentTimeoutJob>>());
    }

    // ── Arrange helpers ───────────────────────────────────────────────────────

    private async Task<(Product Product, Order Order, Payment Payment)> SeedCardOrderAsync(
        int stock            = 10,
        int minutesOld       = 31,
        bool alreadyCancelled = false,
        string paymentMethod = "card")
    {
        var user = User.Create("Test", $"{Guid.NewGuid()}@example.com", "Password1", null);
        var product = Product.Create("Widget", "Desc", 50m, stock, Category.Electronics);
        await SaveAsync(user, product);

        var snapshot = AddressSnapshot.From(
            Address.Create(user.Id, "John", "01012345678", "Egypt",
                "Cairo", "Nasr City", "Street 9", "12", "3", "7", "Home", true));

        var order = Order.Create(user.Id, $"Order #{Guid.NewGuid():N}", snapshot);
        var item  = OrderItem.Create(order.Id, product.Id, 1, product.Price);
        order.AddItem(item);
        order.SetTotalAmount(product.Price);

        if (alreadyCancelled)
            order.Cancel(isAdmin: true);

        await SaveAsync(order);

        var payment = Payment.Create(order.Id, "cs_test_session_123", 50m, paymentMethod);
        await SaveAsync(payment);

        // Backdate payment to simulate elapsed time
        await DbContext.Payments
            .Where(p => p.Id == payment.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(
                p => p.CreatedAt, DateTimeOffset.UtcNow.AddMinutes(-minutesOld)));

        // Reload the product so the test can inspect stock changes via the same reference
        var freshProduct = (await DbContext.Products.AsNoTracking().SingleAsync(p => p.Id == product.Id));
        return (freshProduct, order, payment);
    }

    // ── Timeout fires ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WhenCardPaymentTimedOut_ShouldCancelOrder()
    {
        var (_, order, _) = await SeedCardOrderAsync(minutesOld: 31);

        await _job.ExecuteAsync();

        var updated = await DbContext.Orders.FindAsync(order.Id);
        updated!.Status.ShouldBe(OrderStatus.Cancelled);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCardPaymentTimedOut_ShouldMarkPaymentFailed()
    {
        var (_, _, payment) = await SeedCardOrderAsync(minutesOld: 31);

        await _job.ExecuteAsync();

        var updated = await DbContext.Payments.FindAsync(payment.Id);
        updated!.Status.ShouldBe(PaymentStatus.Failed);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCardPaymentTimedOut_ShouldRestoreStock()
    {
        var (product, _, _) = await SeedCardOrderAsync(stock: 10, minutesOld: 31);
        // Stock was decremented to 9 by the order creation (1 unit reserved)
        await DbContext.Products
            .Where(p => p.Id == product.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, 9));

        await _job.ExecuteAsync();

        var updated = await DbContext.Products.AsNoTracking().SingleAsync(p => p.Id == product.Id);
        updated.StockQuantity.ShouldBe(10); // fully restored
    }

    // ── Timeout does NOT fire ─────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WhenCodPayment_ShouldNotCancelOrder()
    {
        var (_, order, _) = await SeedCardOrderAsync(minutesOld: 60, paymentMethod: "cash_on_delivery");

        await _job.ExecuteAsync();

        var updated = await DbContext.Orders.FindAsync(order.Id);
        updated!.Status.ShouldBe(OrderStatus.Placed); // unchanged
    }

    [Fact]
    public async Task ExecuteAsync_WhenPaymentNotYetTimedOut_ShouldNotCancelOrder()
    {
        var (_, order, _) = await SeedCardOrderAsync(minutesOld: 10); // only 10 minutes old

        await _job.ExecuteAsync();

        var updated = await DbContext.Orders.FindAsync(order.Id);
        updated!.Status.ShouldBe(OrderStatus.Placed);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOrderAlreadyCancelled_ShouldSkipCompletely()
    {
        await SeedCardOrderAsync(minutesOld: 31, alreadyCancelled: true);

        await _job.ExecuteAsync();

        // No refund attempted on an already-cancelled order
        await _stripeMock.DidNotReceive()
            .RefundAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoTimedOutPayments_ShouldDoNothing()
    {
        await _job.ExecuteAsync();

        await _stripeMock.DidNotReceive()
            .RefundAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ── Exactly at boundary ───────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WhenPaymentIsExactly30MinutesOld_ShouldNotTimeout()
    {
        // The cutoff is strictly less-than, so exactly 30 minutes should be safe
        var (_, order, _) = await SeedCardOrderAsync(minutesOld: 29);

        await _job.ExecuteAsync();

        var updated = await DbContext.Orders.FindAsync(order.Id);
        updated!.Status.ShouldBe(OrderStatus.Placed);
    }
}