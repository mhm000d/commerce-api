namespace Commerce.Application.Models;

public enum OrderStatus
{
    Placed,
    Paid,
    Shipped,
    Delivered,
    Cancelled
}

/// <summary>
/// Orders NEVER reference the Address table directly — the selected address
/// is copied into this snapshot at checkout to preserve historical accuracy.
/// </summary>
public class AddressSnapshot
{
    public string  FullName       { get; private set; } = null!;
    public string  PhoneNumber    { get; private set; } = null!;
    public string  Country        { get; private set; } = null!;
    public string  Governorate    { get; private set; } = null!;
    public string  Area           { get; private set; } = null!;
    public string  Street         { get; private set; } = null!;
    public string? BuildingNumber { get; private set; }
    public string? Floor          { get; private set; }
    public string? Apartment      { get; private set; }
    public string? AddressName { get; private set; } // e.g. "Home", "Work"
    
    // private AddressSnapshot() { }   // EF Core

    public static AddressSnapshot From(Address address)
    {
        return new AddressSnapshot
        {
            FullName       = address.FullName,
            PhoneNumber    = address.PhoneNumber,
            Country        = address.Country,
            Governorate    = address.Governorate,
            Area           = address.Area,
            Street         = address.Street,
            BuildingNumber = address.BuildingNumber,
            Floor          = address.Floor,
            Apartment      = address.Apartment,
            AddressName    = address.AddressName
        };
    }
}