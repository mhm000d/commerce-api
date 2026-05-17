namespace Commerce.Contracts.Orders;

// PaymentMethod accepted values: "card" | "cash_on_delivery"
// Parsed/validated in the controller before hitting the service.
public record CheckoutRequest(Guid AddressId, string PaymentMethod);

// NewStatus is accepted as a string to avoid Contracts depending on the domain layer.
// Parsing + validation happens in the controller.
public record UpdateOrderStatusRequest(string NewStatus);