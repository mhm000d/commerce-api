using System.Security.Claims;
using Commerce.Api.Mappings;
using Commerce.Application.Services.Addresses;
using Commerce.Contracts.Addresses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers;

[ApiController]
[Authorize]
public class AddressesController(IAddressService addressService) : ControllerBase
{
    [HttpGet(ApiEndpoints.Addresses.GetAddresses)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var userId = GetUserId();

        var addresses = await addressService.GetAddressesAsync(userId, ct);
        var response = addresses.Select(a => a.ToResponse());

        return Ok(response.ToList());
    }

    [HttpPost(ApiEndpoints.Addresses.PostAddress)]
    public async Task<IActionResult> Post(
        [FromBody] AddressRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();

        var address = await addressService.CreateAddressAsync(
            userId,
            request.FullName,
            request.PhoneNumber,
            request.Country,
            request.Governorate,
            request.Area,
            request.Street,
            request.BuildingNumber,
            request.Floor,
            request.Apartment,
            request.AddressName,
            request.IsDefault,
            ct);

        return StatusCode(201, address.ToResponse());
    }
    
    [HttpPut(ApiEndpoints.Addresses.PutAddress)]
    public async Task<IActionResult> Put(
        Guid id,
        [FromBody] AddressRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        
        var address = await addressService.UpdateAddressAsync(
            id,
            userId,
            request.FullName,
            request.PhoneNumber,
            request.Country,
            request.Governorate,
            request.Area,
            request.Street,
            request.BuildingNumber,
            request.Floor,
            request.Apartment,
            request.AddressName,
            request.IsDefault,
            ct);
        
        return Ok(address.ToResponse());
    }
    
    [HttpDelete(ApiEndpoints.Addresses.DeleteAddress)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();

        await addressService.DeleteAddressAsync(id, userId, ct);

        return NoContent();
    }


    // ── Private helpers ───────────────────────────────────────────────────────
    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}