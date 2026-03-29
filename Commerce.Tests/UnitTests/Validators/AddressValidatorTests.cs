using Commerce.Application.Models;
using Commerce.Application.Validators;
using Shouldly;

namespace Commerce.Tests.UnitTests.Validators;

public class AddressValidatorTests
{
    private readonly AddressValidator _validator = new();

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Builds a fully valid Address so individual tests can break just one field.
    private static Address ValidAddress(
        string  fullName       = "John Doe",
        string  phoneNumber    = "+201012345678",
        string  country        = "Egypt",
        string  governorate    = "Cairo",
        string  area           = "Nasr City",
        string  street         = "Abbas El Akkad St",
        string? buildingNumber = "12",
        string? floor          = "3",
        string? apartment      = "5",
        string? addressName    = "Home")
        => Address.Create(
            userId: Guid.NewGuid(),
            fullName: fullName,
            phoneNumber: phoneNumber,
            country: country,
            governorate: governorate,
            area: area,
            street: street,
            buildingNumber: buildingNumber,
            floor: floor,
            apartment: apartment,
            addressName: addressName);

    // ── FullName ──────────────────────────────────────────────────────────────

    [Fact]
    public void FullName_WhenEmpty_ShouldFail()
    {
        var address = ValidAddress(fullName: "");
        var result  = _validator.Validate(address);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(Address.FullName));
    }

    [Fact]
    public void FullName_WhenWhitespaceOnly_ShouldFail()
    {
        // The validator has .Must(x => !string.IsNullOrWhiteSpace(x)) on FullName
        var address = ValidAddress(fullName: "   ");
        var result  = _validator.Validate(address);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(Address.FullName));
    }

    [Fact]
    public void FullName_WhenAtMaxLength_ShouldNotFail()
    {
        var address = ValidAddress(fullName: new string('A', 200));
        var result  = _validator.Validate(address);

        result.Errors.ShouldNotContain(e => e.PropertyName == nameof(Address.FullName));
    }

    [Fact]
    public void FullName_WhenOverMaxLength_ShouldFail()
    {
        var address = ValidAddress(fullName: new string('A', 201));
        var result  = _validator.Validate(address);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(Address.FullName));
    }

    // ── PhoneNumber ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("+201012345678")]   // Egyptian mobile with country code
    [InlineData("01012345678")]    // Local format, 11 digits
    [InlineData("+12025550104")]   // US format
    public void PhoneNumber_WhenValidFormat_ShouldNotFail(string phone)
    {
        var address = ValidAddress(phoneNumber: phone);
        var result  = _validator.Validate(address);

        result.Errors.ShouldNotContain(e => e.PropertyName == nameof(Address.PhoneNumber));
    }

    [Theory]
    [InlineData("abc")]            // letters
    [InlineData("123")]            // too short (< 7 digits)
    [InlineData("++1234567")]      // double plus
    [InlineData("12 34 56 789")]  // spaces
    public void PhoneNumber_WhenInvalidFormat_ShouldFail(string phone)
    {
        var address = ValidAddress(phoneNumber: phone);
        var result  = _validator.Validate(address);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(Address.PhoneNumber));
    }

    [Fact]
    public void PhoneNumber_WhenEmpty_ShouldFail()
    {
        var address = ValidAddress(phoneNumber: "");
        var result  = _validator.Validate(address);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(Address.PhoneNumber));
    }

    // ── Required string fields (Country, Governorate, Area, Street) ───────────
    // Parameterized to avoid copy-pasting the same test four times.

    [Theory]
    [InlineData("Country")]
    [InlineData("Governorate")]
    [InlineData("Area")]
    [InlineData("Street")]
    public void RequiredField_WhenEmpty_ShouldFail(string fieldName)
    {
        var address = fieldName switch
        {
            "Country"     => ValidAddress(country:     ""),
            "Governorate" => ValidAddress(governorate: ""),
            "Area"        => ValidAddress(area:        ""),
            "Street"      => ValidAddress(street:      ""),
            _             => throw new ArgumentOutOfRangeException(fieldName)
        };

        var result = _validator.Validate(address);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == fieldName);
    }

    // ── Optional fields — null is always valid ────────────────────────────────

    [Fact]
    public void OptionalFields_WhenNull_ShouldNotFail()
    {
        // All optional fields absent — still a valid address.
        var address = ValidAddress(
            buildingNumber: null,
            floor:          null,
            apartment:      null,
            addressName:    null);

        var result = _validator.Validate(address);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("BuildingNumber", 21)]
    [InlineData("Floor",          21)]
    [InlineData("Apartment",      21)]
    public void OptionalField_WhenOverMaxLength_ShouldFail(string fieldName, int length)
    {
        var over = new string('x', length);

        var address = fieldName switch
        {
            "BuildingNumber" => ValidAddress(buildingNumber: over),
            "Floor"          => ValidAddress(floor:          over),
            "Apartment"      => ValidAddress(apartment:      over),
            _                => throw new ArgumentOutOfRangeException(fieldName)
        };

        var result = _validator.Validate(address);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == fieldName);
    }

    [Fact]
    public void AddressName_WhenOverMaxLength_ShouldFail()
    {
        var address = ValidAddress(addressName: new string('x', 256));
        var result  = _validator.Validate(address);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(Address.AddressName));
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public void Address_WithAllValidFields_ShouldPassValidation()
    {
        var result = _validator.Validate(ValidAddress());
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Address_WithOnlyRequiredFields_ShouldPassValidation()
    {
        // Optional fields are null — must still be valid.
        var result = _validator.Validate(ValidAddress(
            buildingNumber: null,
            floor:          null,
            apartment:      null,
            addressName:    null));

        result.IsValid.ShouldBeTrue();
    }
}