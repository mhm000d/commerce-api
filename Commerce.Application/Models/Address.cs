namespace Commerce.Application.Models;

public class Address
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string FullName { get; private set; } = null!;
    public string PhoneNumber { get; private set; } = null!;
    public string Country { get; private set; } = null!;
    public string Governorate { get; private set; } = null!;
    public string Area { get; private set; } = null!;
    public string Street { get; private set; } = null!;
    public string? BuildingNumber { get; private set; }
    public string? Floor { get; private set; }
    public string? Apartment { get; private set; }
    public string? AddressName { get; private set; } // e.g. "Home", "Work"
    public bool IsDefault { get; private set; }

    // ── Navigation Properties ─────────────────────────────────────────────────
    public User User { get; private set; } = null!;

    // ── Factory ───────────────────────────────────────────────────────────────
    public static Address Create(
        Guid userId,
        string fullName,
        string phoneNumber,
        string country,
        string governorate,
        string area,
        string street,
        string? buildingNumber = null,
        string? floor = null,
        string? apartment = null,
        string? addressName = null,
        bool isDefault = false)
    {
        return new Address
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FullName = fullName,
            PhoneNumber = phoneNumber,
            Country = country,
            Governorate = governorate,
            Area = area,
            Street = street,
            BuildingNumber = buildingNumber,
            Floor = floor,
            Apartment = apartment,
            AddressName = addressName,
            IsDefault = isDefault,
        };
    }

    // ── Behaviour ─────────────────────────────────────────────────────────────
    public void Update(
        string fullName, string phoneNumber, string country, string governorate,
        string area, string street, string? buildingNumber, string? floor,
        string? apartment, string? addressName)
    {
        FullName = fullName;
        PhoneNumber = phoneNumber;
        Country = country;
        Governorate = governorate;
        Area = area;
        Street = street;
        BuildingNumber = buildingNumber;
        Floor = floor;
        Apartment = apartment;
        AddressName = addressName;
    }

    /// <summary>Only one address per user should be default.</summary>
    public void SetAsDefault() => IsDefault = true;

    public void UnsetDefault() => IsDefault = false;

    /// <summary>creates an immutable snapshot for Order.ShippingAddressSnapshot.</summary>
    public AddressSnapshot ToSnapshot() => AddressSnapshot.From(this);
}