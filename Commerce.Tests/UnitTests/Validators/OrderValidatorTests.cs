using Commerce.Application.Models;
using Commerce.Application.Validators;
using Shouldly;

namespace Commerce.Tests.UnitTests.Validators;

public class OrderValidatorTests
{
    private readonly OrderValidator _validator = new();

    // ── TotalAmount ───────────────────────────────────────────────────────────

    [Fact]
    public void TotalAmount_WhenNegative_ShouldFail()
    {
        var order = BuildOrder(total: -0.01m);
        var result = _validator.Validate(order);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(Order.TotalAmount));
    }

    [Fact]
    public void TotalAmount_WhenZero_ShouldPass()
    {
        // Free orders (e.g. full discount) are valid domain-wise.
        var order = BuildOrder(total: 0m);
        var result = _validator.Validate(order);

        result.Errors.ShouldNotContain(e => e.PropertyName == nameof(Order.TotalAmount));
    }

    // ── Items ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Items_WhenEmpty_ShouldFail()
    {
        var order = BuildOrder();
        var result = _validator.Validate(order);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(Order.Items));
    }

    [Fact]
    public void Items_WithOneValidItem_ShouldPassItemsRule()
    {
        var order = BuildOrder();
        AddItemTo(order, quantity: 1, unitPrice: 10m);

        var result = _validator.Validate(order);

        result.Errors.ShouldNotContain(e => e.PropertyName == nameof(Order.Items));
    }

    // ── OrderItemValidator (via RuleForEach) ───────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void OrderItem_WhenQuantityNotPositive_ShouldFail(int invalidQuantity)
    {
        var order = BuildOrder();
        AddItemTo(order, quantity: invalidQuantity, unitPrice: 10m);

        var result = _validator.Validate(order);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName.Contains(nameof(OrderItem.Quantity)));
    }

    [Fact]
    public void OrderItem_WhenUnitPriceNegative_ShouldFail()
    {
        var order = BuildOrder();
        AddItemTo(order, quantity: 1, unitPrice: -0.01m);

        var result = _validator.Validate(order);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName.Contains(nameof(OrderItem.UnitPrice)));
    }

    // ── ShippingAddressSnapshot ────────────────────────────────────────────────

    [Fact]
    public void ShippingAddress_WhenNull_ShouldFail()
    {
        var order = BuildOrderWithNullAddress();
        var result = _validator.Validate(order);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e =>
            e.PropertyName == nameof(Order.ShippingAddressSnapshot));
    }

    // ── Full happy path ────────────────────────────────────────────────────────

    [Fact]
    public void Order_WithValidItemsAndAddress_ShouldPassValidation()
    {
        var order = BuildOrder(total: 49.99m);
        AddItemTo(order, quantity: 2, unitPrice: 24.995m);

        var result = _validator.Validate(order);

        result.IsValid.ShouldBeTrue();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Order BuildOrder(decimal total = 100m)
    {
        var address = BuildAddress();
        var snapshot = AddressSnapshot.From(address);
        var order = Order.Create(Guid.NewGuid(), "Order #001000001", snapshot);
        order.SetTotalAmount(total);
        return order;
    }

    private static Order BuildOrderWithNullAddress()
    {
        var order = Order.Create(Guid.NewGuid(), "Order #001000002",
            default(AddressSnapshot)!);
        order.SetTotalAmount(10m);

        typeof(Order)
            .GetProperty(nameof(Order.ShippingAddressSnapshot))!
            .SetValue(order, null);

        return order;
    }

    private static void AddItemTo(Order order, int quantity, decimal unitPrice)
    {
        var item = OrderItem.Create(order.Id, Guid.NewGuid(), quantity, unitPrice);
        order.AddItem(item);
    }

    private static Address BuildAddress() =>
        Address.Create(
            userId: Guid.NewGuid(),
            fullName: "John Doe",
            phoneNumber: "01012345678",
            country: "Egypt",
            governorate: "Cairo",
            area: "Nasr City",
            street: "Street 9",
            buildingNumber: "12",
            floor: "3",
            apartment: "7",
            addressName: "Home",
            isDefault: true);
}