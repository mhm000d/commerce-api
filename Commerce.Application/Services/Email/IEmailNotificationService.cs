namespace Commerce.Application.Services.Email;

public interface IEmailNotificationService
{
    Task QueueOrderConfirmationAsync(
        string recipientEmail,
        string customerName,
        string orderNumber,
        string orderId,
        decimal totalAmount,
        IEnumerable<OrderLineItemData> items,
        CancellationToken ct = default);

    Task QueuePasswordResetAsync(
        string recipientEmail,
        string rawToken,
        CancellationToken ct = default);
}

/// <summary>
/// Serialized into TemplateData["Items"] as a JSON array.
/// </summary>
public record OrderLineItemData(
    string ProductName,
    string? ImageUrl,
    decimal UnitPrice,
    int Quantity)
{
    public decimal LineTotal => UnitPrice * Quantity;
}