namespace Commerce.Application.Models;

public class Order
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string OrderNumber { get; private set; } = null!;
    public OrderStatus Status { get; private set; }
    public decimal TotalAmount { get; private set; }
    public bool ConfirmationEmailSent { get; private set; }
    public DateTimeOffset? ConfirmationEmailSentAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public AddressSnapshot ShippingAddressSnapshot { get; private set; } = null!;

    // ── Navigation Properties ─────────────────────────────────────────────────
    public User User { get; private set; } = null!;
    public ICollection<OrderItem> Items { get; private set; } = [];
    public Payment? Payment { get; private set; }
    public ICollection<EmailNotification> EmailNotifications { get; private set; } = [];

    // ── Factory ───────────────────────────────────────────────────────────────
    public static Order Create(Guid userId, string orderNumber, AddressSnapshot shippingAddress)
    {
        return new Order
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrderNumber = orderNumber,
            Status = OrderStatus.Placed,
            ShippingAddressSnapshot = shippingAddress,
            ConfirmationEmailSent = false,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    // ── State Machine ─────────────────────────────────────────────────────────
    public void MarkAsPaid()
    {
        EnsureTransition(from: OrderStatus.Placed, to: OrderStatus.Paid);
        Status = OrderStatus.Paid;
    }

    public void MarkAsShipped()
    {
        EnsureTransition(from: OrderStatus.Paid, to: OrderStatus.Shipped);
        Status = OrderStatus.Shipped;
    }

    public void MarkAsDelivered()
    {
        EnsureTransition(from: OrderStatus.Shipped, to: OrderStatus.Delivered);
        Status = OrderStatus.Delivered;
    }
    
    public void Cancel(bool isAdmin = false)
    {
        var canCancel = isAdmin
            ? Status != OrderStatus.Delivered
            : Status is OrderStatus.Placed or OrderStatus.Paid;

        if (!canCancel)
            throw new InvalidOperationException(
                $"Order '{OrderNumber}' cannot be cancelled from status '{Status}'.");

        Status = OrderStatus.Cancelled;
    }

    public void MarkConfirmationEmailSent()
    {
        ConfirmationEmailSent = true;
        ConfirmationEmailSentAt = DateTimeOffset.UtcNow;
    }

    public void AddItem(OrderItem item) => Items.Add(item);

    // ── Helpers ───────────────────────────────────────────────────────────────
    private void EnsureTransition(OrderStatus from, OrderStatus to)
    {
        if (Status != from)
            throw new InvalidOperationException(
                $"Invalid order transition: '{Status}' → '{to}'. Expected current status: '{from}'.");
    }

    public void SetTotalAmount(decimal total) => TotalAmount = total;
}