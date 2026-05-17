using Commerce.Application.Models;
using Commerce.Contracts.Common;
using Commerce.Contracts.Orders;

namespace Commerce.Api.Mappings;

public static class OrderMappings
{
    public static CheckoutResponse ToCheckoutResponse(this Order order, string? stripeClientSecret) => new(
        OrderId:           order.Id,
        OrderNumber:       order.OrderNumber,
        TotalAmount:       order.TotalAmount,
        StripeClientSecret: stripeClientSecret); // null for COD — frontend branches on this
    
    // Lightweight summary for paginated lists — doesn't project Product.Name,
    // so no ThenInclude(Product) is needed when fetching these.
    public static OrderSummaryResponse ToSummaryResponse(this Order order) => new(
        Id:          order.Id,
        OrderNumber: order.OrderNumber,
        Status:      order.Status.ToString(),
        TotalAmount: order.TotalAmount,
        ItemCount:   order.Items.Count,
        CreatedAt:   order.CreatedAt);
    
    public static PagedResponse<OrderSummaryResponse> ToPagedResponse(
        this IEnumerable<Order> orders, int page, int pageSize, int totalCount)
    {
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        return new PagedResponse<OrderSummaryResponse>(
            Data: orders.Select(o => o.ToSummaryResponse()).ToList(),
            Pagination: new PaginationMeta(
                Page:         page,
                PageSize:     pageSize,
                TotalItems:   totalCount,
                TotalPages:   totalPages,
                HasNext:      page < totalPages,
                HasPrevious:  page > 1));
    }
    
    public static OrderResponse ToResponse(this Order order) => new(
        Id:                    order.Id,
        OrderNumber:           order.OrderNumber,
        Status:                order.Status.ToString(),
        TotalAmount:           order.TotalAmount,
        ShippingAddress:       order.ShippingAddressSnapshot.ToResponse(),
        Items:                 order.Items.Select(i => i.ToResponse()).ToList(),
        Payment:               order.Payment?.ToResponse(),
        ConfirmationEmailSent: order.ConfirmationEmailSent,
        CreatedAt:             order.CreatedAt);
    
    public static AddressSnapshotResponse ToResponse(this AddressSnapshot s) => new(
        FullName:       s.FullName,
        PhoneNumber:    s.PhoneNumber,
        Country:        s.Country,
        Governorate:    s.Governorate,
        Area:           s.Area,
        Street:         s.Street,
        BuildingNumber: s.BuildingNumber,
        Floor:          s.Floor,
        Apartment:      s.Apartment,
        AddressName:    s.AddressName);
    
    public static OrderItemResponse ToResponse(this OrderItem item) => new(
        Id:          item.Id,
        ProductId:   item.ProductId,
        ProductName: item.Product!.Name,
        PrimaryImageUrl: item.Product.Images.FirstOrDefault()?.ImageUrl,
        Quantity:    item.Quantity,
        UnitPrice:   item.UnitPrice,
        LineTotal:   item.LineTotal);

    public static PaymentResponse ToResponse(this Payment payment) => new(
        Id:            payment.Id,
        Status:        payment.Status.ToString(),
        Amount:        payment.Amount,
        PaymentMethod: payment.PaymentMethod,
        CreatedAt:     payment.CreatedAt);
}