namespace Commerce.Contracts.Addresses;

public record AddressResponse(
    Guid    Id,
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