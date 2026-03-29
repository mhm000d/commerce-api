using Commerce.Application.Models;
using Commerce.Application.Validators;
using Shouldly;

namespace Commerce.Tests.UnitTests.Validators;

public class CartValidatorTests
{
    private readonly CartValidator _validator = new();

    // ── UserId ────────────────────────────────────────────────────────────────

    [Fact]
    public void UserId_WhenEmpty_ShouldFail()
    {
        // Cart.Create prevents an empty UserId at runtime, so we build
        // the state that the validator is guarding against via reflection.
        var cart = BuildCartWithUserId(Guid.Empty);
        var result = _validator.Validate(cart);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(Cart.UserId));
    }

    [Fact]
    public void UserId_WhenValid_ShouldNotFailOnUserId()
    {
        var cart = Cart.Create(Guid.NewGuid());
        var result = _validator.Validate(cart);

        result.Errors.ShouldNotContain(e => e.PropertyName == nameof(Cart.UserId));
    }

    // ── Duplicate products ────────────────────────────────────────────────────

    [Fact]
    public void Items_WhenDuplicateProductIds_ShouldFail()
    {
        // AddOrUpdateItem merges duplicates automatically, so this guard only
        // fires if items are constructed externally (e.g. seeded directly in DB).
        // The validator still protects the domain invariant.
        var cart = Cart.Create(Guid.NewGuid());
        var sharedProductId = Guid.NewGuid();

        // Force two items with the same ProductId via the internal factory.
        cart.AddOrUpdateItem(sharedProductId, 1, 10m);

        // Manually add a second item bypassing AddOrUpdateItem using reflection.
        var duplicate = BuildCartItem(cart.Id, sharedProductId, 2, 10m);
        AddItemDirectly(cart, duplicate);

        var result = _validator.Validate(cart);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(Cart.Items));
    }

    [Fact]
    public void Items_WhenEmpty_ShouldPassCartValidation()
    {
        // An empty cart is valid — user may just be browsing.
        var cart = Cart.Create(Guid.NewGuid());
        var result = _validator.Validate(cart);

        result.IsValid.ShouldBeTrue();
    }

    // ── CartItemValidator (via RuleForEach) ────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1000)]
    public void CartItem_WhenQuantityOutOfRange_ShouldFail(int invalidQuantity)
    {
        var cart = Cart.Create(Guid.NewGuid());
        var item = BuildCartItem(cart.Id, Guid.NewGuid(), invalidQuantity, 10m);
        AddItemDirectly(cart, item);

        var result = _validator.Validate(cart);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName.Contains(nameof(CartItem.Quantity)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(500)]
    [InlineData(999)]
    public void CartItem_WhenQuantityInRange_ShouldNotFailOnQuantity(int validQuantity)
    {
        var cart = Cart.Create(Guid.NewGuid());
        // AddOrUpdateItem enforces the 999 cap so we build directly for boundary tests.
        var item = BuildCartItem(cart.Id, Guid.NewGuid(), validQuantity, 10m);
        AddItemDirectly(cart, item);

        var result = _validator.Validate(cart);

        result.Errors.ShouldNotContain(e => e.PropertyName.Contains(nameof(CartItem.Quantity)));
    }

    [Fact]
    public void CartItem_WhenUnitPriceSnapshotNegative_ShouldFail()
    {
        var cart = Cart.Create(Guid.NewGuid());
        var item = BuildCartItem(cart.Id, Guid.NewGuid(), 1, -0.01m);
        AddItemDirectly(cart, item);

        var result = _validator.Validate(cart);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e =>
            e.PropertyName.Contains(nameof(CartItem.UnitPriceSnapshot)));
    }

    [Fact]
    public void CartItem_WhenUnitPriceSnapshotIsZero_ShouldPass()
    {
        // Free products are technically valid (discount could bring price to 0).
        var cart = Cart.Create(Guid.NewGuid());
        var item = BuildCartItem(cart.Id, Guid.NewGuid(), 1, 0m);
        AddItemDirectly(cart, item);

        var result = _validator.Validate(cart);

        result.Errors.ShouldNotContain(e =>
            e.PropertyName.Contains(nameof(CartItem.UnitPriceSnapshot)));
    }

    // ── Full happy path ────────────────────────────────────────────────────────

    [Fact]
    public void Cart_WithValidItems_ShouldPassValidation()
    {
        var cart = Cart.Create(Guid.NewGuid());
        cart.AddOrUpdateItem(Guid.NewGuid(), 2, 49.99m);
        cart.AddOrUpdateItem(Guid.NewGuid(), 1, 9.99m);

        var result = _validator.Validate(cart);

        result.IsValid.ShouldBeTrue();
    }

    // ── Reflection helpers ────────────────────────────────────────────────────
    // Used to force invalid states that the domain model actively prevents
    // in normal operation — this tests the validator's own rules in isolation.

    private static Cart BuildCartWithUserId(Guid userId)
    {
        var cart = Cart.Create(Guid.NewGuid());
        typeof(Cart)
            .GetProperty(nameof(Cart.UserId))!
            .SetValue(cart, userId);
        return cart;
    }

    private static CartItem BuildCartItem(
        Guid cartId, Guid productId, int quantity, decimal price)
    {
        // CartItem.Create is internal — access via reflection.
        var method = typeof(CartItem)
            .GetMethod("Create",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static)!;

        return (CartItem)method.Invoke(null, [cartId, productId, quantity, price])!;
    }

    private static void AddItemDirectly(Cart cart, CartItem item)
    {
        var itemsProp = typeof(Cart)
            .GetProperty(nameof(Cart.Items))!
            .GetValue(cart) as ICollection<CartItem>;

        itemsProp!.Add(item);
    }
}