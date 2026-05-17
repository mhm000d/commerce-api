namespace Commerce.Contracts.Orders;

public record OrderResponse(
    Guid                    Id,
    string                  OrderNumber,
    string                  Status,
    decimal                 TotalAmount,
    AddressSnapshotResponse ShippingAddress,
    List<OrderItemResponse> Items,
    PaymentResponse?        Payment,
    bool                    ConfirmationEmailSent,
    DateTimeOffset          CreatedAt);
    
// Lightweight summary used in paginated list responses — avoids loading full item graph.
public record OrderSummaryResponse(
    Guid           Id,
    string         OrderNumber,
    string         Status,
    decimal        TotalAmount,
    int            ItemCount,
    DateTimeOffset CreatedAt);

public record CheckoutResponse(
    Guid    OrderId,
    string  OrderNumber,
    decimal TotalAmount,
    string? StripeClientSecret); // null for Cash on Delivery

public record CheckoutSessionStatusResponse(string Status, string? CustomerEmail);

public record AddressSnapshotResponse(
    string  FullName,
    string  PhoneNumber,
    string  Country,
    string  Governorate,
    string  Area,
    string  Street,
    string? BuildingNumber,
    string? Floor,
    string? Apartment,
    string? AddressName);

public record OrderItemResponse(
    Guid    Id,
    Guid    ProductId,
    string  ProductName,
    string? PrimaryImageUrl,
    int     Quantity,
    decimal UnitPrice,
    decimal LineTotal);    

public record PaymentResponse(
    Guid    Id,
    string  Status,
    decimal Amount,
    string  PaymentMethod,
    DateTimeOffset CreatedAt);
    
