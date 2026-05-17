using System.Text.Json;
using Commerce.Application.Models;
using Commerce.Application.Services.Email;
using Commerce.Application.Services.Email.Templates;
using Commerce.Application.Settings;
using Commerce.Tests.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Commerce.Tests.IntegrationTests.Services;

[Collection("Database")]
public class EmailNotificationServiceTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private EmailNotificationService _service = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        _service = new EmailNotificationService(
            DbContext,
            Options.Create(new EmailSettings
            {
                FromAddress     = "noreply@commerce.com",
                FromName        = "Commerce",
                FrontendBaseUrl = "https://app.commerce.com"
            }));
    }

    // ── QueueOrderConfirmationAsync ───────────────────────────────────────────

    [Fact]
    public async Task QueueOrderConfirmation_ShouldPersistWithCorrectTemplateAndStatus()
    {
        var order = await SeedMinimalOrderAsync();

        await _service.QueueOrderConfirmationAsync(
            recipientEmail: "customer@example.com",
            customerName:   "John Doe",
            orderNumber:    "Order #000000001",
            orderId:        order.Id.ToString(),
            totalAmount:    99.99m,
            items:          []);

        var notification = await DbContext.EmailNotifications
            .SingleAsync(n => n.RecipientEmail == "customer@example.com");

        notification.Template.ShouldBe(EmailTemplate.OrderConfirmation);
        notification.Status.ShouldBe(EmailStatus.Pending);
        notification.Attempts.ShouldBe(0);
        notification.MaxAttempts.ShouldBe(3);
        notification.OrderId.ShouldBe(order.Id);
    }

    [Fact]
    public async Task QueueOrderConfirmation_ShouldStoreAllTemplateData()
    {
        await _service.QueueOrderConfirmationAsync(
            recipientEmail: "customer@example.com",
            customerName:   "Jane Doe",
            orderNumber:    "Order #000000007",
            orderId:        null!,
            totalAmount:    249.99m,
            items:          []);

        var notification = await DbContext.EmailNotifications
            .SingleAsync(n => n.RecipientEmail == "customer@example.com");
        // DbContext.Update(notification);
        
        notification.TemplateData["CustomerName"].ShouldBe("Jane Doe");
        notification.TemplateData["OrderNumber"].ShouldBe("Order #000000007");
        notification.TemplateData["TotalAmount"].ShouldBe("249.99");
    }

    [Fact]
    public async Task QueueOrderConfirmation_ShouldSerializeLineItemsIntoTemplateData()
    {
        var items = new List<OrderLineItemData>
        {
            new("Sony Headphones", "https://img.example.com/sony.jpg", 299.99m, 2)
        };

        await _service.QueueOrderConfirmationAsync(
            "customer@example.com", "John", "Order #2",
            null!, 599.98m, items);

        var notification = await DbContext.EmailNotifications
            .SingleAsync(n => n.RecipientEmail == "customer@example.com");

        notification.TemplateData.ContainsKey("Items").ShouldBeTrue();

        var deserialized = JsonSerializer
            .Deserialize<List<OrderLineItemData>>(notification.TemplateData["Items"])!;

        deserialized.Count.ShouldBe(1);
        deserialized[0].ProductName.ShouldBe("Sony Headphones");
        deserialized[0].Quantity.ShouldBe(2);
        deserialized[0].UnitPrice.ShouldBe(299.99m);
    }

    // ── QueuePasswordResetAsync ───────────────────────────────────────────────

    [Fact]
    public async Task QueuePasswordReset_ShouldPersistWithCorrectTemplateAndNullOrderId()
    {
        await _service.QueuePasswordResetAsync(
            recipientEmail: "user@example.com",
            rawToken:       "raw-reset-token-abc");

        var notification = await DbContext.EmailNotifications
            .SingleAsync(n => n.RecipientEmail == "user@example.com");

        notification.Template.ShouldBe(EmailTemplate.PasswordReset);
        notification.Status.ShouldBe(EmailStatus.Pending);
        notification.OrderId.ShouldBeNull();
    }

    [Fact]
    public async Task QueuePasswordReset_ShouldBuildResetUrlContainingToken()
    {
        await _service.QueuePasswordResetAsync("user@example.com", "my-token-xyz");

        var notification = await DbContext.EmailNotifications
            .SingleAsync(n => n.RecipientEmail == "user@example.com");

        notification.TemplateData["ResetUrl"].ShouldContain("my-token-xyz");
        notification.TemplateData["ResetUrl"].ShouldContain("https://app.commerce.com");
        notification.TemplateData["ResetUrl"].ShouldContain("/reset-password");
    }

    [Fact]
    public async Task QueuePasswordReset_ShouldUrlEncodeToken()
    {
        // Tokens that contain special characters must be URL-safe
        await _service.QueuePasswordResetAsync("user@example.com", "token with spaces & chars=1");

        var notification = await DbContext.EmailNotifications
            .SingleAsync(n => n.RecipientEmail == "user@example.com");

        notification.TemplateData["ResetUrl"].ShouldNotContain(" ");
        notification.TemplateData["ResetUrl"].ShouldNotContain("&chars=1");
    }

    [Fact]
    public async Task QueuePasswordReset_ShouldStoreExpiryHint()
    {
        await _service.QueuePasswordResetAsync("user@example.com", "token");

        var notification = await DbContext.EmailNotifications
            .SingleAsync(n => n.RecipientEmail == "user@example.com");

        notification.TemplateData["ExpiresIn"].ShouldBe("1 hour");
    }
    
    // ── Private helpers ───────────────────────────────────────────────────────
    private async Task<Order> SeedMinimalOrderAsync()
    {
        var user = User.Create("Test", $"{Guid.NewGuid()}@example.com", "Password1", null);
        await SaveAsync(user);

        var address = Address.Create(user.Id, "John", "01012345678",
            "Egypt", "Cairo", "Nasr City", "Street 9", "12", "3", "7", "Home", true);

        var snapshot = AddressSnapshot.From(address);
        var order = Order.Create(user.Id, "Order #000000001", snapshot);
        order.SetTotalAmount(99.99m);
        await SaveAsync(order);

        return order;
    }
}