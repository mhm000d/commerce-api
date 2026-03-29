using Commerce.Application.Database;
using Commerce.Application.Exceptions;
using Commerce.Application.Models;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Commerce.Application.Services.Addresses;

public class AddressService(
    AppDbContext dbContext,
    IValidator<Address> addressValidator,
    ILogger<AddressService> logger) : IAddressService
{
    public async Task<IReadOnlyList<Address>> GetAddressesAsync(Guid userId, CancellationToken ct = default)
    {
        return await dbContext.Addresses
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault) // default address always first
            .ToListAsync(ct);
    }

    public async Task<Address> CreateAddressAsync(Guid userId, string fullName, string phoneNumber, string country,
        string governorate,
        string area, string street, string? buildingNumber, string? floor, string? apartment, string? addressName,
        bool isDefault, CancellationToken ct = default)
    {
        // first address is always default, no matter what the user sent.
        var existingCount = await dbContext.Addresses
            .CountAsync(a => a.UserId == userId, ct);

        var willBeDefault = isDefault || existingCount == 0;

        var address = Address.Create(
            userId, fullName, phoneNumber, country, governorate, area, street,
            buildingNumber, floor, apartment, addressName, isDefault: willBeDefault);

        await addressValidator.ValidateAndThrowAsync(address, ct);

        await using var tx = await dbContext.Database.BeginTransactionAsync(ct);

        // clear any existing default before setting the new one.
        if (willBeDefault)
            await ClearDefaultsAsync(userId, excludeId: null, ct);

        dbContext.Addresses.Add(address);
        await dbContext.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);

        logger.LogInformation(
            "Address created. AddressId={AddressId} UserId={UserId} IsDefault={IsDefault}",
            address.Id, userId, willBeDefault);

        return address;
    }

    public async Task<Address> UpdateAddressAsync(Guid addressId, Guid userId, string fullName, string phoneNumber,
        string country,
        string governorate, string area, string street, string? buildingNumber, string? floor, string? apartment,
        string? addressName, bool isDefault, CancellationToken ct = default)
    {
        var address = await dbContext.Addresses
                          .FirstOrDefaultAsync(a => a.Id == addressId, ct)
                      ?? throw new NotFoundException("Address not found.", "ADDRESS_NOT_FOUND");

        // Ownership check — users can only edit their own addresses.
        if (address.UserId != userId)
            throw new ForbiddenException(
                "You can only edit your own addresses.", "FORBIDDEN");

        address.Update(
            fullName, phoneNumber, country, governorate,
            area, street, buildingNumber, floor, apartment, addressName);

        await addressValidator.ValidateAndThrowAsync(address, ct);

        await using var tx = await dbContext.Database.BeginTransactionAsync(ct);

        if (isDefault && !address.IsDefault)
        {
            // becoming the new default — clear siblings first.
            await ClearDefaultsAsync(userId, excludeId: addressId, ct);
            address.SetAsDefault();
        }
        else if (!isDefault && address.IsDefault)
        {
            address.UnsetDefault();
        }

        await dbContext.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        logger.LogInformation(
            "Address updated. AddressId={AddressId} UserId={UserId} IsDefault={IsDefault}",
            addressId, userId, address.IsDefault);

        return address;
    }

    public async Task DeleteAddressAsync(Guid addressId, Guid userId, CancellationToken ct = default)
    {
        var address = await dbContext.Addresses
                          .FirstOrDefaultAsync(a => a.Id == addressId, ct)
                      ?? throw new NotFoundException("Address not found.", "ADDRESS_NOT_FOUND");

        if (address.UserId != userId)
            throw new ForbiddenException(
                "You can only delete your own addresses.", "FORBIDDEN");

        await using var tx = await dbContext.Database.BeginTransactionAsync(ct);

        dbContext.Addresses.Remove(address);
        await dbContext.SaveChangesAsync(ct);

        // if we just deleted the default, promote the next newest.
        if (address.IsDefault)
            await PromoteNewDefaultAsync(userId, ct);

        await tx.CommitAsync(ct);

        logger.LogInformation(
            "Address deleted. AddressId={AddressId} UserId={UserId}",
            addressId, userId);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Unsets IsDefault on all of a user's addresses except the one being
    /// promoted (if any).
    /// </summary>
    private async Task ClearDefaultsAsync(
        Guid userId,
        Guid? excludeId,
        CancellationToken ct)
    {
        var current = await dbContext.Addresses
            .Where(a => a.UserId == userId && a.IsDefault &&
                        (excludeId == null || a.Id != excludeId))
            .ToListAsync(ct);

        foreach (var a in current)
            a.UnsetDefault();
    }

    private async Task PromoteNewDefaultAsync(Guid userId, CancellationToken ct)
    {
        var next = await dbContext.Addresses
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.Id)
            .FirstOrDefaultAsync(ct);

        if (next is null) return;

        next.SetAsDefault();
        await dbContext.SaveChangesAsync(ct);
    }
}