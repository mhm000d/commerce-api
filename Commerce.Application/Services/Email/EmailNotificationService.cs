using System.Text.Json;
using Commerce.Application.Database;
using Commerce.Application.Models;
using Commerce.Application.Settings;
using Microsoft.Extensions.Options;

namespace Commerce.Application.Services.Email;

public class EmailNotificationService(
    AppDbContext dbContext,
    IOptions<EmailSettings> settings) : IEmailNotificationService
{
    private readonly EmailSettings _settings = settings.Value;

    public async Task QueueOrderConfirmationAsync(string recipientEmail, string customerName,
        string orderNumber, string orderId, decimal totalAmount,
        IEnumerable<OrderLineItemData> items, CancellationToken ct = default)
    {
        var notification = EmailNotification.Create(
            recipientEmail: recipientEmail,
            template: EmailTemplate.OrderConfirmation,
            templateData: new Dictionary<string, string>
            {
                ["CustomerName"] = customerName,
                ["OrderNumber"]  = orderNumber,
                ["OrderId"]      = orderId,
                ["TotalAmount"]  = totalAmount.ToString("F2"),
                ["Items"]        = JsonSerializer.Serialize(items)
            },
            orderId: Guid.TryParse(orderId, out var id) ? id : null);

        dbContext.EmailNotifications.Add(notification);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task QueuePasswordResetAsync(string recipientEmail, string rawToken, CancellationToken ct = default)
    {
        var resetUrl = $"{_settings.FrontendBaseUrl}/reset-password?token={Uri.EscapeDataString(rawToken)}";

        var notification = EmailNotification.Create(
            recipientEmail: recipientEmail,
            template: EmailTemplate.PasswordReset,
            templateData: new Dictionary<string, string>
            {
                ["ResetUrl"]  = resetUrl,
                ["ExpiresIn"] = "1 hour"
            });

        dbContext.EmailNotifications.Add(notification);
        await dbContext.SaveChangesAsync(ct);
    }
}