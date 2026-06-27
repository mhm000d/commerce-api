using Commerce.Application.Models;
using Commerce.Contracts.Orders;

namespace Commerce.Application.Services.Orders;

public interface IOrderService
{
    Task<(Order Order, string? StripeCheckoutUrl)> CheckoutAsync(
        Guid userId,
        Guid addressId,
        CheckoutPaymentMethod paymentMethod,
        CancellationToken ct = default);

    public Task<CheckoutSessionStatusResponse> GetCheckoutSessionStatusAsync(
        string sessionId, CancellationToken ct = default);

    Task<Order> GetOrderAsync(
        Guid userId, Guid orderId, CancellationToken ct = default);

    Task<(IEnumerable<Order> Orders, int TotalCount)> GetOrdersAsync(
        Guid userId, int page, int pageSize, CancellationToken ct = default);

    Task<Order> CancelOrderAsync(
        Guid userId, Guid orderId, CancellationToken ct = default);

    Task<(string ClientSecret, string SessionId)> RetryPaymentAsync(
        Guid userId,
        Guid orderId,
        CancellationToken ct = default);
}