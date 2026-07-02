using System.Data;
using Commerce.Application.Database;
using Commerce.Application.Exceptions;
using Commerce.Application.Models;
using Commerce.Application.Services.Email;
using Commerce.Application.Services.Payments;
using Commerce.Contracts.Orders;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ValidationException = Commerce.Application.Exceptions.ValidationException;

namespace Commerce.Application.Services.Orders;

public class OrderService(
    AppDbContext dbContext,
    IStripeService stripeService,
    IEmailNotificationService emailService,
    IValidator<Order> orderValidator,
    IConfiguration configuration,
    ILogger<OrderService> logger) : IOrderService
{
    public async Task<(Order Order, string? StripeCheckoutUrl)> CheckoutAsync(
        Guid userId, Guid addressId, CheckoutPaymentMethod paymentMethod, CancellationToken ct = default)
    {
        var cart = await dbContext.Carts
                       .Include(c => c.Items)
                       .ThenInclude(i => i.Product)
                       .ThenInclude(p => p.Images)
                       .IgnoreQueryFilters()           // ← see deleted products so we can give a proper error
                       .Where(c => c.UserId == userId) // ← re-apply the UserId filter manually since IgnoreQueryFilters drops everything
                       .FirstOrDefaultAsync(c => c.UserId == userId, ct)
                   ?? throw new NotFoundException("Cart not found.", "CART_NOT_FOUND");

        logger.LogInformation("Checkout: UserId={UserId}, CartId={CartId}, ItemCount={Count}", userId, cart?.Id, cart?.Items?.Count);
        
        if (!cart.Items.Any())
            throw new ValidationException("Cart is empty.", "CART_EMPTY");

        var address = await dbContext.Addresses
                          .FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == userId, ct)
                      ?? throw new NotFoundException("Address not found.", "ADDRESS_NOT_FOUND");

        // Load user email — needed to pre-fill Stripe's checkout page.
        var user = await dbContext.Users.FindAsync([userId], ct)
                   ?? throw new NotFoundException("User not found.", "USER_NOT_FOUND");

        // Pre-flight stock check before opening the transaction.
        foreach (var cartItem in cart.Items)
        {
            if (cartItem.Product.IsDeleted)
                throw new ConflictException(
                    $"'{cartItem.Product.Name}' is no longer available.",
                    "PRODUCT_UNAVAILABLE");

            if (cartItem.Product.StockQuantity < cartItem.Quantity)
                throw new ConflictException(
                    $"Insufficient stock for '{cartItem.Product.Name}'. " +
                    $"Available: {cartItem.Product.StockQuantity}.",
                    "INSUFFICIENT_STOCK");
        }

        var lineItemSnapshots = cart.Items.Select(i => new CheckoutLineItem(
            ProductName: i.Product.Name,
            PrimaryImageUrl: i.Product.Images.FirstOrDefault(img => img.IsPrimary)?.ImageUrl
                             ?? i.Product.Images.FirstOrDefault()?.ImageUrl,
            UnitPrice: i.Product.Price,
            Quantity: i.Quantity)).ToList();

        // ── DB Transaction ────────────────────────────────────────────────────
        // Creates the order, reserves stock, and clears the cart atomically.
        var orderNumber = await GenerateOrderNumberAsync(ct);
        Order order;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            var snapshot = AddressSnapshot.From(address);
            order = Order.Create(userId, orderNumber, snapshot);
            dbContext.Orders.Add(order);

            decimal total = 0;
            foreach (var cartItem in cart.Items)
            {
                var orderItem = OrderItem.Create(
                    order.Id,
                    cartItem.ProductId,
                    cartItem.Quantity,
                    cartItem.Product.Price);

                order.AddItem(orderItem);
                
                cartItem.Product.DecreaseStock(cartItem.Quantity);

                total += orderItem.UnitPrice * orderItem.Quantity;
            }

            order.SetTotalAmount(total);

            await orderValidator.ValidateAndThrowAsync(order, ct);

            if (paymentMethod == CheckoutPaymentMethod.CashOnDelivery)
            {
                cart.Clear();
            }

            try
            {
                await dbContext.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ConflictException(
                    "Stock was updated during checkout. Please try again.",
                    "CONCURRENCY_CONFLICT");
            }

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }

        logger.LogInformation(
            "Order placed. OrderId={OrderId} OrderNumber={OrderNumber} " +
            "PaymentMethod={PaymentMethod} Total={Total}",
            order.Id, orderNumber, paymentMethod, order.TotalAmount);

        // ── Payment — branched by method ──────────────────────────────────────────
        string? stripeClientSecret = null;

        if (paymentMethod == CheckoutPaymentMethod.CashOnDelivery)
        {
            // COD: Payment record exists for bookkeeping
            // but uses a sentinel ProviderId. The order stays in PLACED until
            // an admin marks it PAID after physical collection.
            var codPayment = Payment.Create(
                orderId: order.Id,
                paymentProviderId: "COD",
                amount: order.TotalAmount,
                paymentMethod: "cash_on_delivery");

            dbContext.Payments.Add(codPayment);
        }
        else // Card
        {
            // {CHECKOUT_SESSION_ID} is a Stripe literal placeholder — Stripe replaces
            // it with the real session ID in the redirect URL automatically.
            // Card branch — swap success+cancel URLs for a single returnUrl
            var returnUrl = $"{configuration["Frontend:BaseUrl"]}/order-return?session_id={{CHECKOUT_SESSION_ID}}";

            var (sessionId, clientSecret) = await stripeService.CreateCheckoutSessionAsync(
                orderId: order.Id,
                orderNumber: orderNumber,
                customerEmail: user.Email,
                lineItems: lineItemSnapshots,
                returnUrl: returnUrl,
                ct: ct);

            stripeClientSecret = clientSecret;

            // Store the Checkout Session ID now. The webhook will update this to
            // the PaymentIntentId once checkout.session.completed fires, because
            // RefundAsync needs the PaymentIntentId.
            var cardPayment = Payment.Create(
                orderId: order.Id,
                paymentProviderId: sessionId,
                amount: order.TotalAmount,
                paymentMethod: "card");

            dbContext.Payments.Add(cardPayment);
        }

        await dbContext.SaveChangesAsync(ct);

        if (paymentMethod == CheckoutPaymentMethod.CashOnDelivery)
        {
            await QueueOrderConfirmationAsync(order, user, lineItemSnapshots, ct);
        }

        await LoadOrderNavigationsAsync(order, ct);
        return (order, stripeClientSecret);
    }

    public async Task<CheckoutSessionStatusResponse> GetCheckoutSessionStatusAsync(
        string sessionId, CancellationToken ct = default)
    {
        var session = await stripeService.GetSessionStatusAsync(sessionId, ct);
        var payment = await dbContext.Payments
            .FirstOrDefaultAsync(p => p.PaymentProviderId == sessionId, ct);
        
        return new CheckoutSessionStatusResponse(session.Status, session.CustomerEmail, payment?.OrderId);
    }

    public async Task<Order> GetOrderAsync(Guid userId, Guid orderId, CancellationToken ct = default)
    {
        return await dbContext.Orders
                   .Include(o => o.Items)
                   .ThenInclude(i => i.Product)
                   .ThenInclude(p => p.Images.Where(img => img.IsPrimary))
                   .Include(o => o.Payment)
                   .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId, ct)
               ?? throw new NotFoundException("Order not found.", "ORDER_NOT_FOUND");
    }

    public async Task<(IEnumerable<Order> Orders, int TotalCount)> GetOrdersAsync(
        Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = dbContext.Orders
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt);

        var totalCount = await query.CountAsync(ct);

        var orders = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(o => o.Items)
            .ToListAsync(ct);

        return (orders, totalCount);
    }

    public async Task<Order> CancelOrderAsync(Guid userId, Guid orderId, CancellationToken ct = default)
    {
        var order = await dbContext.Orders
                        .Include(o => o.Items)
                        .ThenInclude(i => i.Product)
                        .Include(o => o.Payment)
                        .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId, ct)
                    ?? throw new NotFoundException("Order not found.", "ORDER_NOT_FOUND");

        try
        {
            order.Cancel(isAdmin: false);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message, "INVALID_ORDER_TRANSITION");
        }
        
        await RestoreStockAndRefundAsync(order, ct);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "Order cancelled by customer. OrderId={OrderId} UserId={UserId}", orderId, userId);

        return order;
    }
    
public async Task<(string ClientSecret, string SessionId)> RetryPaymentAsync(
    Guid userId, Guid orderId, CancellationToken ct = default)
{
    var order = await dbContext.Orders
        .Include(o => o.Items)
        .ThenInclude(i => i.Product)
        .ThenInclude(p => p.Images.Where(img => img.IsPrimary))
        .Include(o => o.Payment)
        .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId, ct)
        ?? throw new NotFoundException("Order not found.", "ORDER_NOT_FOUND");

    if (order.Status != OrderStatus.Placed)
        throw new ValidationException("Order cannot be retried.", "INVALID_ORDER_STATE");

    if (order.Payment?.PaymentMethod != "card")
        throw new ValidationException("Only card payments can be retried.", "INVALID_PAYMENT_METHOD");

    var lineItems = order.Items.Select(i => new CheckoutLineItem(
        ProductName: i.Product!.Name,
        PrimaryImageUrl: i.Product.Images.FirstOrDefault()?.ImageUrl,
        UnitPrice: i.UnitPrice,
        Quantity: i.Quantity)).ToList();

    var user = await dbContext.Users.FindAsync([userId], ct)
               ?? throw new NotFoundException("User not found.", "USER_NOT_FOUND");

    var returnUrl = $"{configuration["Frontend:BaseUrl"]}/order-return?session_id={{CHECKOUT_SESSION_ID}}";

    var (sessionId, clientSecret) = await stripeService.CreateCheckoutSessionAsync(
        orderId: order.Id,
        orderNumber: order.OrderNumber,
        customerEmail: user.Email,
        lineItems: lineItems,
        returnUrl: returnUrl,
        ct: ct);

    order.Payment.UpdateProviderId(sessionId);
    order.Payment.MarkPending();
    
    await dbContext.SaveChangesAsync(ct);

    logger.LogInformation(
        "Retry payment session created. OrderId={OrderId} SessionId={SessionId}",
        order.Id, sessionId);

    return (clientSecret, sessionId);
}

    // ── Private Helpers ───────────────────────────────────────────────────────

    private async Task QueueOrderConfirmationAsync(
        Order order,
        User user,
        IEnumerable<CheckoutLineItem> lineItems,
        CancellationToken ct)
    {
        var emailLineItems = lineItems.Select(i => new OrderLineItemData(
            ProductName: i.ProductName,
            ImageUrl: i.PrimaryImageUrl,
            UnitPrice: i.UnitPrice,
            Quantity: i.Quantity));

        await emailService.QueueOrderConfirmationAsync(
            user.Email,
            user.Name,
            order.OrderNumber,
            order.Id.ToString(),
            order.TotalAmount,
            emailLineItems,
            ct,
            paymentMethod: "Cash on delivery",
            paymentStatus: "Awaiting payment");
    }

    /// <summary>
    /// Shared cancel logic: restores product stock and initiates a Stripe refund
    /// if the payment was already completed.
    /// </summary>
    private async Task RestoreStockAndRefundAsync(Order order, CancellationToken ct)
    {
        foreach (var item in order.Items)
            item.Product!.RestoreStock(item.Quantity);

        if (order.Payment?.Status == PaymentStatus.Completed)
        {
            await stripeService.RefundAsync(order.Payment.PaymentProviderId, ct);
            order.Payment.MarkRefunded();

            logger.LogInformation(
                "Refund initiated. OrderId={OrderId} PaymentId={PaymentId}",
                order.Id, order.Payment.Id);
        }
    }

    private async Task LoadOrderNavigationsAsync(Order order, CancellationToken ct)
    {
        if (!dbContext.Entry(order).Collection(o => o.Items).IsLoaded)
            await dbContext.Entry(order).Collection(o => o.Items).LoadAsync(ct);

        foreach (var item in order.Items)
        {
            if (!dbContext.Entry(item).Reference(i => i.Product).IsLoaded)
                await dbContext.Entry(item).Reference(i => i.Product).LoadAsync(ct);

            if (!dbContext.Entry(item.Product).Collection(p => p.Images).IsLoaded)
                await dbContext.Entry(item.Product)
                    .Collection(p => p.Images)
                    .LoadAsync(ct);
        }

        if (!dbContext.Entry(order).Reference(o => o.Payment).IsLoaded)
            await dbContext.Entry(order).Reference(o => o.Payment).LoadAsync(ct);
    }

    /// <summary>
    /// Generates a sequential public order number using a PostgreSQL sequence.
    /// </summary>
    private async Task<string> GenerateOrderNumberAsync(CancellationToken ct)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT nextval('order_number_seq')";
        var seq = (long)(await cmd.ExecuteScalarAsync(ct))!;
        return $"{seq:D9}"; // "001000001"
    }
}
