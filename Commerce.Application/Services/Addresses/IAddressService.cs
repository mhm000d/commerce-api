using Commerce.Application.Models;

namespace Commerce.Application.Services.Addresses;

public interface IAddressService
{
    Task<IReadOnlyList<Address>> GetAddressesAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<Address> CreateAddressAsync(
        Guid    userId, string  fullName, string  phoneNumber,
        string  country, string  governorate, string  area, string  street,
        string? buildingNumber, string? floor, string? apartment, string? addressName,
        bool    isDefault, CancellationToken ct = default);
    
    Task<Address> UpdateAddressAsync(
        Guid addressId, Guid userId, string  fullName, string  phoneNumber,
        string  country, string  governorate, string  area, string  street,
        string? buildingNumber, string? floor, string? apartment, string? addressName,
        bool    isDefault, CancellationToken ct = default);
    
    Task DeleteAddressAsync(Guid addressId, Guid userId, CancellationToken ct = default);
}