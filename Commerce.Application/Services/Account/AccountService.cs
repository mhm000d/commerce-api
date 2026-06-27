using Commerce.Application.Database;
using Commerce.Application.Exceptions;
using Commerce.Application.Models;
using Commerce.Contracts.Account;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Commerce.Application.Services.Account;

public class AccountService(
    AppDbContext dbContext,
    IValidator<UpdateProfileRequest> updateProfileValidator,
    IValidator<ChangePasswordRequest> changePasswordValidator,
    ILogger<AccountService> logger) : IAccountService
{
    public async Task<User> GetProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException("User not found.", "USER_NOT_FOUND");

        return user;
    }

    public async Task<User> UpdateProfileAsync(Guid userId, string name, string? phone, CancellationToken ct = default)
    {
        // Validate request
        var request = new UpdateProfileRequest(name, phone);
        await updateProfileValidator.ValidateAndThrowAsync(request, ct);

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException("User not found.", "USER_NOT_FOUND");

        user.UpdateProfile(name, phone);

        // Save changes
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Profile updated for user {UserId}", userId);
        return user;
    }

    public async Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        // Validate request
        var request = new ChangePasswordRequest(currentPassword, newPassword);
        await changePasswordValidator.ValidateAndThrowAsync(request, ct);

        var user = await dbContext.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException("User not found.", "USER_NOT_FOUND");

        // Verify current password
        if (!user.VerifyPassword(currentPassword))
            throw new UnauthorizedException("Current password is incorrect.", "INVALID_PASSWORD");

        // Update password
        user.UpdatePassword(newPassword);

        // Revoke all refresh tokens to force re‑login (security best practice)
        var activeTokens = user.RefreshTokens.Where(rt => rt.IsActive).ToList();
        foreach (var token in activeTokens)
            token.Revoke(RevokeReasons.PasswordReset);

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Password changed for user {UserId}", userId);
    }
}