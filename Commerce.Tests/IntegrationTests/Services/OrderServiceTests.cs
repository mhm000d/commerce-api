using System.Text.Json;
using Commerce.Application.Exceptions;
using Commerce.Application.Models;
using Commerce.Application.Services.Email;
using Commerce.Application.Services.Orders;
using Commerce.Application.Services.Payments;
using Commerce.Application.Settings;
using Commerce.Application.Validators;
using Commerce.Tests.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Commerce.Tests.IntegrationTests.Services;

public class OrderServiceTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private OrderService _orderService = null!;
    private IStripeService _stripeMock = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        _stripeMock = Substitute.For<IStripeService>();

        // Default card checkout returns a fake session — overridden per-test as needed.
        _stripeMock
            .CreateCheckoutSessionAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IEnumerable<CheckoutLineItem>>(), Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(("cs_test_session123", "cs_test_client_secret_abc"));

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Frontend:BaseUrl"] = "http://localhost:3000"
            })
            .Build();

        _orderService = new OrderService(
            dbContext:      DbContext,
            stripeService:  _stripeMock,
            emailService:   new EmailNotificationService(
                DbContext,
                Options.Create(new EmailSettings
                {
                    FromAddress = "noreply@commerce.local",
                    FromName = "Commerce",
                    FrontendBaseUrl = "http://localhost:3000"
                })),
            orderValidator: new OrderValidator(),
            configuration:  config,
            logger:         Substitute.For<ILogger<OrderService>>());
    }

    // ── Arrange helpers ───────────────────────────────────────────────────────

    private async Task<User> CreateUserAsync(string email = "user@example.com")
    {
        var user = User.Create("Test User", email, "Password1", phone: null);
        await SaveAsync(user);
        return user;
    }

    private async Task<Address> CreateAddressAsync(Guid userId)
    {
        var address = Address.Create(
            userId:         userId,
            fullName:       "John Doe",
            phoneNumber:    "01012345678",
            country:        "Egypt",
            governorate:    "Cairo",
            area:           "Nasr City",
            street:         "Street 9",
            buildingNumber: "12",
            floor:          "3",
            apartment:      "7",
            addressName:    "Home",
            isDefault:      true);
        await SaveAsync(address);
        return address;
    }

    private async Task<Product> CreateProductAsync(decimal price = 29.99m, int stock = 10)
    {
        var product = Product.Create(
            name:          "Test Product",
            description:   "A product.",
            price:         price,
            stockQuantity: stock,
            category:      Category.Electronics);
        await SaveAsync(product);
        return product;
    }

    private async Task AddToCartAsync(Guid userId, Guid productId, int quantity = 1)
    {
        var cart = await DbContext.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (cart is null)
        {
            cart = Cart.Create(userId);
            DbContext.Carts.Add(cart);
        }

        var product = await DbContext.Products.FindAsync(productId);
        cart.AddOrUpdateItem(productId, quantity, product!.Price);
        await DbContext.SaveChangesAsync();

        // Detach so service loads fresh
        foreach (var e in DbContext.ChangeTracker.Entries().ToList())
            e.State = Microsoft.EntityFrameworkCore.EntityState.Detached;
    }

    private async Task<(User User, Address Address, Product Product)> ArrangeCheckoutAsync(
        int stock = 10, decimal price = 50m)
    {
        var user    = await CreateUserAsync();
        var address = await CreateAddressAsync(user.Id);
        var product = await CreateProductAsync(price: price, stock: stock);
        await AddToCartAsync(user.Id, product.Id, quantity: 1);
        return (user, address, product);
    }

    // ── CheckoutAsync — COD ───────────────────────────────────────────────────

    [Fact]
    public async Task Checkout_COD_ShouldCreateOrderAndPaymentWithoutCallingStripe()
    {
        var (user, address, product) = await ArrangeCheckoutAsync(price: 50m);

        var (order, clientSecret) = await _orderService.CheckoutAsync(
            user.Id, address.Id, CheckoutPaymentMethod.CashOnDelivery);

        // No Stripe call
        await _stripeMock.DidNotReceive().CreateCheckoutSessionAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IEnumerable<CheckoutLineItem>>(), Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        clientSecret.ShouldBeNull();
        order.Status.ShouldBe(OrderStatus.Placed);
        order.TotalAmount.ShouldBe(50m);

        var payment = await DbContext.Payments.SingleAsync(p => p.OrderId == order.Id);
        payment.PaymentProviderId.ShouldBe("COD");
        payment.PaymentMethod.ShouldBe("cash_on_delivery");
        payment.Status.ShouldBe(PaymentStatus.Pending);
    }

    [Fact]
    public async Task Checkout_COD_ShouldQueueOrderConfirmationEmail()
    {
        var (user, address, product) = await ArrangeCheckoutAsync(price: 50m);

        var (order, _) = await _orderService.CheckoutAsync(
            user.Id, address.Id, CheckoutPaymentMethod.CashOnDelivery);

        var notification = await DbContext.EmailNotifications
            .SingleAsync(n => n.OrderId == order.Id);

        notification.RecipientEmail.ShouldBe(user.Email);
        notification.Template.ShouldBe(EmailTemplate.OrderConfirmation);
        notification.Status.ShouldBe(EmailStatus.Pending);
        notification.TemplateData["CustomerName"].ShouldBe(user.Name);
        notification.TemplateData["OrderNumber"].ShouldBe(order.OrderNumber);
        notification.TemplateData["TotalAmount"].ShouldBe("50.00");
        notification.TemplateData["PaymentMethod"].ShouldBe("Cash on delivery");
        notification.TemplateData["PaymentStatus"].ShouldBe("Awaiting payment");

        var items = JsonSerializer.Deserialize<List<OrderLineItemData>>(
            notification.TemplateData["Items"])!;

        items.Count.ShouldBe(1);
        items[0].ProductName.ShouldBe(product.Name);
        items[0].UnitPrice.ShouldBe(50m);
        items[0].Quantity.ShouldBe(1);
    }

    [Fact]
    public async Task Checkout_COD_ShouldDecrementStockAndClearCart()
    {
        var (user, address, product) = await ArrangeCheckoutAsync(stock: 10, price: 50m);

        await _orderService.CheckoutAsync(
            user.Id, address.Id, CheckoutPaymentMethod.CashOnDelivery);

        var updatedProduct = await DbContext.Products.AsNoTracking()
            .SingleAsync(p => p.Id == product.Id);
        updatedProduct.StockQuantity.ShouldBe(9); // 10 - 1

        var cart = await DbContext.Carts
            .Include(c => c.Items)
            .SingleAsync(c => c.UserId == user.Id);
        cart.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Checkout_COD_ShouldSnapshotShippingAddress()
    {
        var (user, address, _) = await ArrangeCheckoutAsync();

        var (order, _) = await _orderService.CheckoutAsync(
            user.Id, address.Id, CheckoutPaymentMethod.CashOnDelivery);

        order.ShippingAddressSnapshot.FullName.ShouldBe(address.FullName);
        order.ShippingAddressSnapshot.Street.ShouldBe(address.Street);
    }

    // ── CheckoutAsync — Card ──────────────────────────────────────────────────

    [Fact]
    public async Task Checkout_Card_ShouldCallStripeAndReturnClientSecret()
    {
        var (user, address, _) = await ArrangeCheckoutAsync();

        var (order, clientSecret) = await _orderService.CheckoutAsync(
            user.Id, address.Id, CheckoutPaymentMethod.Card);

        clientSecret.ShouldBe("cs_test_client_secret_abc");

        await _stripeMock.Received(1).CreateCheckoutSessionAsync(
            order.Id, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IEnumerable<CheckoutLineItem>>(), Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        var payment = await DbContext.Payments.SingleAsync(p => p.OrderId == order.Id);
        payment.PaymentProviderId.ShouldBe("cs_test_session123");
        payment.PaymentMethod.ShouldBe("card");
    }

    [Fact]
    public async Task Checkout_Card_ShouldNotQueueOrderConfirmationBeforeWebhookCompletes()
    {
        var (user, address, _) = await ArrangeCheckoutAsync();

        var (order, _) = await _orderService.CheckoutAsync(
            user.Id, address.Id, CheckoutPaymentMethod.Card);

        var queuedCount = await DbContext.EmailNotifications
            .CountAsync(n => n.OrderId == order.Id);

        queuedCount.ShouldBe(0);
    }

    [Fact]
    public async Task Checkout_Card_ShouldPassLineItemsWithProductNameToStripe()
    {
        var (user, address, product) = await ArrangeCheckoutAsync(price: 99m);

        await _orderService.CheckoutAsync(
            user.Id, address.Id, CheckoutPaymentMethod.Card);

        await _stripeMock.Received(1).CreateCheckoutSessionAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Is<IEnumerable<CheckoutLineItem>>(items =>
                items.Any(i =>
                    i.ProductName == product.Name &&
                    i.UnitPrice == 99m &&
                    i.Quantity == 1)),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    // ── CheckoutAsync — Guard cases ───────────────────────────────────────────

    [Fact]
    public async Task Checkout_WhenNoCart_ShouldThrowNotFoundException()
    {
        var user    = await CreateUserAsync();
        var address = await CreateAddressAsync(user.Id);
        // Intentionally no cart

        var act = () => _orderService.CheckoutAsync(
            user.Id, address.Id, CheckoutPaymentMethod.CashOnDelivery);

        await act.ShouldThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Checkout_WhenAddressNotOwnedByUser_ShouldThrowNotFoundException()
    {
        var user          = await CreateUserAsync("user@example.com");
        var otherUser     = await CreateUserAsync("other@example.com");
        var otherAddress  = await CreateAddressAsync(otherUser.Id);
        var product       = await CreateProductAsync();
        await AddToCartAsync(user.Id, product.Id);

        var act = () => _orderService.CheckoutAsync(
            user.Id, otherAddress.Id, CheckoutPaymentMethod.CashOnDelivery);

        await act.ShouldThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Checkout_WhenProductOutOfStock_ShouldThrowConflictException()
    {
        var user    = await CreateUserAsync();
        var address = await CreateAddressAsync(user.Id);
        var product = await CreateProductAsync(stock: 1);
        await AddToCartAsync(user.Id, product.Id, quantity: 1);

        // Deplete stock between cart add and checkout
        await DbContext.Products
            .Where(p => p.Id == product.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, 0));

        var act = () => _orderService.CheckoutAsync(
            user.Id, address.Id, CheckoutPaymentMethod.CashOnDelivery);

        await act.ShouldThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Checkout_WhenProductIsDeleted_ShouldThrowConflictException()
    {
        var user    = await CreateUserAsync();
        var address = await CreateAddressAsync(user.Id);
        var product = await CreateProductAsync();
        
        await AddToCartAsync(user.Id, product.Id);
    
        await DbContext.Products
            .Where(p => p.Id == product.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.IsDeleted, true)
                .SetProperty(p => p.DeletedAt, DateTimeOffset.UtcNow));
        
        var act = () => _orderService.CheckoutAsync(
            user.Id, address.Id, CheckoutPaymentMethod.CashOnDelivery);
    
        await act.ShouldThrowAsync<ConflictException>();
    }

    // ── GetOrderAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrder_WhenOwner_ShouldReturnOrderWithItems()
    {
        var (user, address, _) = await ArrangeCheckoutAsync();
        var (placed, _) = await _orderService.CheckoutAsync(
            user.Id, address.Id, CheckoutPaymentMethod.CashOnDelivery);

        var order = await _orderService.GetOrderAsync(user.Id, placed.Id);

        order.ShouldNotBeNull();
        order.Items.ShouldNotBeEmpty();
        order.Payment.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetOrder_WhenWrongUser_ShouldThrowNotFoundException()
    {
        var (user, address, _) = await ArrangeCheckoutAsync();
        var (placed, _) = await _orderService.CheckoutAsync(
            user.Id, address.Id, CheckoutPaymentMethod.CashOnDelivery);

        var act = () => _orderService.GetOrderAsync(Guid.NewGuid(), placed.Id);

        await act.ShouldThrowAsync<NotFoundException>();
    }

    // ── GetOrdersAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrders_ShouldReturnOnlyCurrentUsersOrders()
    {
        var userA = await CreateUserAsync("a@example.com");
        var userB = await CreateUserAsync("b@example.com");

        var addressA = await CreateAddressAsync(userA.Id);
        var addressB = await CreateAddressAsync(userB.Id);

        var productA = await CreateProductAsync();
        await AddToCartAsync(userA.Id, productA.Id);
        await _orderService.CheckoutAsync(userA.Id, addressA.Id, CheckoutPaymentMethod.CashOnDelivery);

        var productB = await CreateProductAsync();
        await AddToCartAsync(userB.Id, productB.Id);
        await _orderService.CheckoutAsync(userB.Id, addressB.Id, CheckoutPaymentMethod.CashOnDelivery);

        var (orders, total) = await _orderService.GetOrdersAsync(userA.Id, page: 1, pageSize: 20);

        total.ShouldBe(1);
        orders.Single().UserId.ShouldBe(userA.Id);
    }

    // ── CancelOrderAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task CancelOrder_WhenPlaced_ShouldCancelAndRestoreStock()
    {
        var (user, address, product) = await ArrangeCheckoutAsync(stock: 10);
        var (placed, _) = await _orderService.CheckoutAsync(
            user.Id, address.Id, CheckoutPaymentMethod.CashOnDelivery);

        await _orderService.CancelOrderAsync(user.Id, placed.Id);

        var order = await DbContext.Orders.FindAsync(placed.Id);
        order!.Status.ShouldBe(OrderStatus.Cancelled);

        var updatedProduct = await DbContext.Products.AsNoTracking()
            .SingleAsync(p => p.Id == product.Id);
        updatedProduct.StockQuantity.ShouldBe(10); // fully restored
    }

    [Fact]
    public async Task CancelOrder_WhenPaidWithCompletedPayment_ShouldThrowConflictException()
    {
        var (user, address, _) = await ArrangeCheckoutAsync();
        var (placed, _) = await _orderService.CheckoutAsync(
            user.Id, address.Id, CheckoutPaymentMethod.Card);

        // Simulate payment completed via webhook
        var payment = await DbContext.Payments.SingleAsync(p => p.OrderId == placed.Id);
        payment.MarkCompleted();
        payment.UpdateProviderId("pi_test_intent_abc");
        placed.MarkAsPaid();
        DbContext.Update(placed);
        DbContext.Update(payment);
        await DbContext.SaveChangesAsync();
        foreach (var e in DbContext.ChangeTracker.Entries().ToList())
            e.State = Microsoft.EntityFrameworkCore.EntityState.Detached;

        var act = () => _orderService.CancelOrderAsync(user.Id, placed.Id);
        await act.ShouldThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task CancelOrder_WhenShipped_ShouldThrowConflictException()
    {
        var (user, address, _) = await ArrangeCheckoutAsync();
        var (placed, _) = await _orderService.CheckoutAsync(
            user.Id, address.Id, CheckoutPaymentMethod.CashOnDelivery);

        // Advance state to SHIPPED
        placed.MarkAsPaid();
        placed.MarkAsShipped();
        DbContext.Update(placed);
        await DbContext.SaveChangesAsync();
        foreach (var e in DbContext.ChangeTracker.Entries().ToList())
            e.State = Microsoft.EntityFrameworkCore.EntityState.Detached;

        var act = () => _orderService.CancelOrderAsync(user.Id, placed.Id);

        await act.ShouldThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task CancelOrder_WhenOrderNotOwnedByUser_ShouldThrowNotFoundException()
    {
        var (user, address, _) = await ArrangeCheckoutAsync();
        var (placed, _) = await _orderService.CheckoutAsync(
            user.Id, address.Id, CheckoutPaymentMethod.CashOnDelivery);

        var act = () => _orderService.CancelOrderAsync(Guid.NewGuid(), placed.Id);

        await act.ShouldThrowAsync<NotFoundException>();
    }
}
