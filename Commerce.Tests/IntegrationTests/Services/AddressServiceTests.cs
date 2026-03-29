using Commerce.Application.Exceptions;
using Commerce.Application.Models;
using Commerce.Application.Services.Addresses;
using Commerce.Application.Validators;
using Commerce.Tests.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Commerce.Tests.IntegrationTests.Services;

public class AddressServiceTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private AddressService _addressService = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        _addressService = new AddressService(
            dbContext: DbContext,
            addressValidator: new AddressValidator(),
            logger: Substitute.For<ILogger<AddressService>>()
        );
    }

    // ── Arrange helpers ───────────────────────────────────────────────────────

    private async Task<User> CreateUserAsync(string email = "user@example.com")
    {
        var user = User.Create("Test User", email, "Password1", phone: null);
        await SaveAsync(user);
        return user;
    }

    // Creates an address directly through the service (not via SaveAsync)
    // so that service-layer logic (e.g. auto-default promotion) is exercised.
    private Task<Address> CreateAddressAsync(
        Guid userId,
        string fullName = "John Doe",
        string phone = "+201012345678",
        bool isDefault = false,
        string? addressName = "Home")
        => _addressService.CreateAddressAsync(
            userId: userId,
            fullName: fullName,
            phoneNumber: phone,
            country: "Egypt",
            governorate: "Cairo",
            area: "Nasr City",
            street: "Abbas El Akkad",
            buildingNumber: null,
            floor: null,
            apartment: null,
            addressName: addressName,
            isDefault: isDefault);

    // ── GetAddressesAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetAddresses_WhenNoneExist_ShouldReturnEmptyList()
    {
        var user = await CreateUserAsync();

        var result = await _addressService.GetAddressesAsync(user.Id);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAddresses_ShouldReturnAllAddressesForUser()
    {
        var user = await CreateUserAsync();
        await CreateAddressAsync(user.Id, addressName: "Home");
        await CreateAddressAsync(user.Id, addressName: "Work");

        var result = await _addressService.GetAddressesAsync(user.Id);

        result.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetAddresses_DefaultAddressShouldComeFirst()
    {
        var user = await CreateUserAsync();

        // First address auto-becomes default (Decision A).
        var first = await CreateAddressAsync(user.Id, addressName: "Home");
        var second = await CreateAddressAsync(user.Id, addressName: "Work", isDefault: true);

        var result = await _addressService.GetAddressesAsync(user.Id);

        // The address explicitly set as default should appear at index 0.
        result[0].Id.ShouldBe(second.Id);
        result[1].Id.ShouldBe(first.Id);
    }

    [Fact]
    public async Task GetAddresses_ShouldNotReturnOtherUsersAddresses()
    {
        var userA = await CreateUserAsync("a@example.com");
        var userB = await CreateUserAsync("b@example.com");

        await CreateAddressAsync(userA.Id);
        await CreateAddressAsync(userB.Id);

        var result = await _addressService.GetAddressesAsync(userA.Id);

        result.Count.ShouldBe(1);
        result[0].UserId.ShouldBe(userA.Id);
    }

    // ── CreateAddressAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAddress_WithValidData_ShouldPersistAddress()
    {
        var user = await CreateUserAsync();

        var result = await CreateAddressAsync(user.Id, fullName: "Jane Doe");

        // Returned object is correct
        result.ShouldNotBeNull();
        result.FullName.ShouldBe("Jane Doe");

        // Row actually in the database
        var saved = await DbContext.Addresses
            .AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == result.Id);
        saved.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateAddress_FirstAddress_ShouldAlwaysBeDefault()
    {
        // Decision A: regardless of the isDefault flag sent in,
        // the very first address a user adds must become their default.
        var user = await CreateUserAsync();

        var address = await CreateAddressAsync(user.Id, isDefault: false);

        address.IsDefault.ShouldBeTrue();

        var saved = await DbContext.Addresses
            .AsNoTracking()
            .SingleAsync(a => a.Id == address.Id);
        saved.IsDefault.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateAddress_WhenSetAsDefault_ShouldClearPreviousDefault()
    {
        // Decision B: creating a new default must unset the old one atomically.
        var user = await CreateUserAsync();

        var first = await CreateAddressAsync(user.Id); // auto-default
        var second = await CreateAddressAsync(user.Id, isDefault: true); // new default

        // Reload both from DB to get ground truth.
        var firstDb = await DbContext.Addresses.AsNoTracking().SingleAsync(a => a.Id == first.Id);
        var secondDb = await DbContext.Addresses.AsNoTracking().SingleAsync(a => a.Id == second.Id);

        firstDb.IsDefault.ShouldBeFalse();
        secondDb.IsDefault.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateAddress_OnlyOneDefaultPerUser_AfterMultipleAdds()
    {
        var user = await CreateUserAsync();

        await CreateAddressAsync(user.Id);
        await CreateAddressAsync(user.Id, isDefault: true);
        await CreateAddressAsync(user.Id, isDefault: true); // third, also requests default

        var allAddresses = await DbContext.Addresses
            .AsNoTracking()
            .Where(a => a.UserId == user.Id)
            .ToListAsync();

        // Invariant: exactly one default at all times.
        allAddresses.Count(a => a.IsDefault).ShouldBe(1);
    }

    [Fact]
    public async Task CreateAddress_WithInvalidData_ShouldThrowValidationException()
    {
        var user = await CreateUserAsync();

        // Empty FullName violates the validator.
        var act = () => _addressService.CreateAddressAsync(
            userId: user.Id, fullName: "", phoneNumber: "+201012345678",
            country: "Egypt", governorate: "Cairo", area: "Nasr City",
            street: "Main St", buildingNumber: null, floor: null,
            apartment: null, addressName: null, isDefault: false);

        await act.ShouldThrowAsync<FluentValidation.ValidationException>();
    }

    // ── UpdateAddressAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAddress_ByOwner_ShouldPersistChanges()
    {
        var user = await CreateUserAsync();
        var address = await CreateAddressAsync(user.Id, fullName: "Old Name");

        var updated = await _addressService.UpdateAddressAsync(
            addressId: address.Id, userId: user.Id,
            fullName: "New Name", phoneNumber: "+201012345678",
            country: "Egypt", governorate: "Cairo", area: "Nasr City",
            street: "New Street", buildingNumber: null, floor: null,
            apartment: null, addressName: "Work", isDefault: false);

        updated.FullName.ShouldBe("New Name");
        updated.Street.ShouldBe("New Street");

        var saved = await DbContext.Addresses.AsNoTracking().SingleAsync(a => a.Id == address.Id);
        saved.FullName.ShouldBe("New Name");
    }

    [Fact]
    public async Task UpdateAddress_WhenNotFound_ShouldThrowNotFoundException()
    {
        var user = await CreateUserAsync();

        var act = () => _addressService.UpdateAddressAsync(
            addressId: Guid.NewGuid(), userId: user.Id,
            fullName: "X", phoneNumber: "+201012345678",
            country: "Egypt", governorate: "Cairo", area: "Nasr City",
            street: "X", buildingNumber: null, floor: null,
            apartment: null, addressName: null, isDefault: false);

        await act.ShouldThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateAddress_WhenNotOwner_ShouldThrowForbiddenException()
    {
        var owner = await CreateUserAsync("owner@example.com");
        var intruder = await CreateUserAsync("intruder@example.com");
        var address = await CreateAddressAsync(owner.Id);

        var act = () => _addressService.UpdateAddressAsync(
            addressId: address.Id, userId: intruder.Id, // wrong user
            fullName: "Hacked", phoneNumber: "+201012345678",
            country: "Egypt", governorate: "Cairo", area: "Nasr City",
            street: "X", buildingNumber: null, floor: null,
            apartment: null, addressName: null, isDefault: false);

        await act.ShouldThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task UpdateAddress_WhenSettingAsDefault_ShouldClearOtherDefaults()
    {
        var user = await CreateUserAsync();
        var first = await CreateAddressAsync(user.Id); // auto-default
        var second = await CreateAddressAsync(user.Id, isDefault: false); // not default

        // Now update second to be the default.
        await _addressService.UpdateAddressAsync(
            addressId: second.Id, userId: user.Id,
            fullName: "Jane", phoneNumber: "+201012345678",
            country: "Egypt", governorate: "Cairo", area: "Nasr City",
            street: "Main", buildingNumber: null, floor: null,
            apartment: null, addressName: null, isDefault: true);

        var firstDb = await DbContext.Addresses.AsNoTracking().SingleAsync(a => a.Id == first.Id);
        var secondDb = await DbContext.Addresses.AsNoTracking().SingleAsync(a => a.Id == second.Id);

        firstDb.IsDefault.ShouldBeFalse();
        secondDb.IsDefault.ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateAddress_WhenRemovingDefault_ShouldAllowIt()
    {
        // The service allows a user to explicitly un-default an address on update.
        // (Unlike delete, there is no auto-promotion — the user made a deliberate choice.)
        var user = await CreateUserAsync();
        var address = await CreateAddressAsync(user.Id); // auto-default

        await _addressService.UpdateAddressAsync(
            addressId: address.Id, userId: user.Id,
            fullName: "Jane", phoneNumber: "+201012345678",
            country: "Egypt", governorate: "Cairo", area: "Nasr City",
            street: "Main", buildingNumber: null, floor: null,
            apartment: null, addressName: null, isDefault: false); // explicit remove

        var saved = await DbContext.Addresses.AsNoTracking().SingleAsync(a => a.Id == address.Id);
        saved.IsDefault.ShouldBeFalse();
    }

    // ── DeleteAddressAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAddress_ByOwner_ShouldRemoveRow()
    {
        var user = await CreateUserAsync();
        var address = await CreateAddressAsync(user.Id);

        await _addressService.DeleteAddressAsync(address.Id, user.Id);

        var exists = await DbContext.Addresses.AnyAsync(a => a.Id == address.Id);
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAddress_WhenNotFound_ShouldThrowNotFoundException()
    {
        var user = await CreateUserAsync();

        var act = () => _addressService.DeleteAddressAsync(Guid.NewGuid(), user.Id);

        await act.ShouldThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeleteAddress_WhenNotOwner_ShouldThrowForbiddenException()
    {
        var owner = await CreateUserAsync("owner@example.com");
        var intruder = await CreateUserAsync("intruder@example.com");
        var address = await CreateAddressAsync(owner.Id);

        var act = () => _addressService.DeleteAddressAsync(address.Id, intruder.Id);

        await act.ShouldThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task DeleteAddress_WhenDefaultDeleted_ShouldPromoteRemainingAddress()
    {
        // Decision C: deleting the default auto-promotes the next newest address.
        var user = await CreateUserAsync();
        var first = await CreateAddressAsync(user.Id); // auto-default
        var second = await CreateAddressAsync(user.Id); // not default

        second.IsDefault.ShouldBeFalse(); // sanity

        await _addressService.DeleteAddressAsync(first.Id, user.Id);

        var promoted = await DbContext.Addresses
            .AsNoTracking()
            .SingleAsync(a => a.Id == second.Id);

        promoted.IsDefault.ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteAddress_WhenLastAddressDeleted_ShouldLeaveNoAddresses()
    {
        // Deleting the only address should not crash — no address to promote.
        var user = await CreateUserAsync();
        var address = await CreateAddressAsync(user.Id);

        await _addressService.DeleteAddressAsync(address.Id, user.Id);

        var count = await DbContext.Addresses.CountAsync(a => a.UserId == user.Id);
        count.ShouldBe(0);
    }

    [Fact]
    public async Task DeleteAddress_WhenNonDefaultDeleted_ShouldNotChangeExistingDefault()
    {
        // Deleting a non-default address must not touch the real default.
        var user = await CreateUserAsync();
        var defaultOne = await CreateAddressAsync(user.Id); // auto-default
        var other = await CreateAddressAsync(user.Id, isDefault: false);

        await _addressService.DeleteAddressAsync(other.Id, user.Id);

        var saved = await DbContext.Addresses.AsNoTracking().SingleAsync(a => a.Id == defaultOne.Id);
        saved.IsDefault.ShouldBeTrue();
    }
}