namespace Commerce.Contracts.Addresses;

public record AddressRequest(
    string  FullName,
    string  PhoneNumber,
    string  Country,
    string  Governorate,
    string  Area,
    string  Street,
    string? BuildingNumber,
    string? Floor,
    string? Apartment,
    string? AddressName,
    bool    IsDefault
);