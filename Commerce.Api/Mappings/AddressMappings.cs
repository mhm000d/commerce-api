using Commerce.Application.Models;
using Commerce.Contracts.Addresses;

namespace Commerce.Api.Mappings;

public static class AddressMappings
{
    public static AddressResponse ToResponse(this Address address) =>
        new(
            address.Id,
            address.FullName,
            address.PhoneNumber,
            address.Country,
            address.Governorate,
            address.Area,
            address.Street,
            address.BuildingNumber,
            address.Floor,
            address.Apartment,
            address.AddressName,
            address.IsDefault
        );
}