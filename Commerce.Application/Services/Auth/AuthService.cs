using Commerce.Application.Database;
using Commerce.Application.Exceptions;
using Commerce.Application.Models;
using Commerce.Application.Services.Email;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ValidationException = Commerce.Application.Exceptions.ValidationException;

namespace Commerce.Application.Services.Auth;

public class AuthService(
    AppDbContext dbContext,
    IValidator<User> userValidator,
    IValidator<RefreshToken> refreshTokenValidator,
    IValidator<PasswordResetToken> passwordResetTokenValidator,
    ITokenService tokenService,
    IEmailNotificationService emailService,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<AuthResult> RegisterAsync(string name, string email, string rawPassword, string? phone)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var emailTaken = await dbContext.Users.AnyAsync(u => u.Email == normalizedEmail);

        if (emailTaken)
            throw new ConflictException("An account with that email already exists.", "EMAIL_TAKEN");

        var user = User.Create(name, normalizedEmail, rawPassword, phone); // hashes internally
        await userValidator.ValidateAndThrowAsync(user);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("User registered. UserId={UserId}", user.Id);
        return await IssueTokenPairAsync(user);
    }

    public async Task<AuthResult> LoginAsync(string email, string rawPassword)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        if (user is null || !user.VerifyPassword(rawPassword))
            throw new UnauthorizedException("Invalid email or password.", "INVALID_CREDENTIALS");

        logger.LogInformation("User logged in. UserId={UserId}", user.Id);
        return await IssueTokenPairAsync(user);
    }

    public async Task<AuthResult> RefreshAsync(string rawRefreshToken)
    {
        var incomingHash = TokenHasher.Hash(rawRefreshToken);

        var existingToken = await dbContext.RefreshTokens
                                .FirstOrDefaultAsync(rt => rt.TokenHash == incomingHash)
                            ?? throw new UnauthorizedException("Refresh token not found.", "INVALID_TOKEN");

        // ── REUSE ATTACK DETECTION ──
        if (existingToken.IsRevoked)
        {
            var familyId = existingToken.FamilyId;
            logger.LogWarning(
                "Refresh token reuse detected. FamilyId={FamilyId} UserId={UserId} " +
                "OriginalReason={Reason}. Revoking entire family.",
                familyId,
                existingToken.UserId,
                existingToken.RevokedReason
            );

            await RevokeFamilyAsync(familyId, RevokeReasons.ReuseDetected);

            throw new UnauthorizedException(
                "Your session has been invalidated due to a security event. Please login again.",
                "SESSION_COMPROMISED"
            );
        }

        if (existingToken.IsExpired)
            throw new UnauthorizedException("Refresh token has expired.", "TOKEN_EXPIRED");

        // ── ROTATE ──
        var user = await dbContext.Users
                       .FirstOrDefaultAsync(u => u.Id == existingToken.UserId)
                   ?? throw new UnauthorizedException("User account not found.", "USER_NOT_FOUND");

        var (newRawToken, newHashToken) = tokenService.GenerateRefreshToken();
        var refreshTokenExpiresAt = tokenService.RefreshTokenExpiresAt();
        var accessTokenExpiresAt = tokenService.AccessTokenExpiresAt();

        var newRefreshToken = RefreshToken.CreateRotated(
            userId: user.Id,
            tokenHash: newHashToken,
            expiresAt: refreshTokenExpiresAt,
            familyId: existingToken.FamilyId
        );

        await refreshTokenValidator.ValidateAndThrowAsync(newRefreshToken);

        dbContext.RefreshTokens.Add(newRefreshToken);

        existingToken.MarkRotated(replacedByTokenId: newRefreshToken.Id);

        await dbContext.SaveChangesAsync();

        logger.LogInformation(
            "Refresh token rotated. UserId={UserId} FamilyId={FamilyId} " +
            "OldId={OldId} NewId={NewId}",
            user.Id, existingToken.FamilyId, existingToken.Id, newRefreshToken.Id
        );

        var accessToken = tokenService.GenerateAccessToken(user);
        return BuildResponse(user, accessToken, newRawToken, refreshTokenExpiresAt, accessTokenExpiresAt);
    }

    public async Task LogoutAsync(string rawRefreshToken)
    {
        var hash = TokenHasher.Hash(rawRefreshToken);
        var token = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == hash);

        if (token is null || !token.IsActive)
            return;

        token.Revoke(RevokeReasons.Logout);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("User logged out. UserId={UserId} TokenId={TokenId}",
            token.UserId, token.Id
        );
    }

    public async Task LogoutAllAsync(Guid userId)
    {
        var now = DateTimeOffset.UtcNow;
        var tokens = await dbContext.RefreshTokens
            .Where(rt => rt.UserId == userId
                         && rt.RevokedAt == null
                         && rt.ExpiresAt > now
            ).ToListAsync();

        foreach (var token in tokens)
            token.Revoke(RevokeReasons.Logout);

        await dbContext.SaveChangesAsync();

        logger.LogInformation("All-device logout. UserId={UserId} TokensRevoked={Count}",
            userId, tokens.Count
        );
    }

    public async Task ForgotPasswordAsync(string email, CancellationToken ct = default)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken: ct);

        if (user is null)
        {
            logger.LogInformation(
                "Password reset requested for unknown email {Email}", email);
            return;
        }

        var (rawToken, tokenHash) = tokenService.GenerateRefreshToken();
        var resetToken = PasswordResetToken.Create(user.Id, tokenHash);

        await passwordResetTokenValidator.ValidateAndThrowAsync(resetToken, cancellationToken: ct);

        dbContext.PasswordResetTokens.Add(resetToken);
        await dbContext.SaveChangesAsync(ct);

        await emailService.QueuePasswordResetAsync(user.Email, rawToken, ct);
    }

    public async Task ResetPasswordAsync(string rawToken, string newRawPassword)
    {
        var tokenHash = TokenHasher.Hash(rawToken);
        var resetToken = await dbContext.PasswordResetTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash
                                      && t.UsedAt == null
                                      && t.ExpiresAt > DateTimeOffset.UtcNow
            ) ?? throw new ValidationException(
            "INVALID_TOKEN", "Invalid or expired password reset token.");

        var user = await dbContext.Users
                       .FirstOrDefaultAsync(u => u.Id == resetToken.UserId)
                   ?? throw new UnauthorizedException("INVALID_TOKEN", "Invalid or expired password reset token.");

        user.UpdatePassword(newRawPassword);
        resetToken.MarkUsed();

        // Revoke ALL active sessions
        var now = DateTimeOffset.UtcNow;
        var activeTokens = await dbContext.RefreshTokens
            .Where(rt => rt.UserId == user.Id
                         && rt.RevokedAt == null
                         && rt.ExpiresAt > now
            ).ToListAsync();

        foreach (var token in activeTokens)
            token.Revoke(RevokeReasons.PasswordReset);

        await dbContext.SaveChangesAsync();

        logger.LogInformation(
            "Password reset complete. UserId={UserId} SessionsRevoked={Count}",
            user.Id, activeTokens.Count
        );
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────
    private async Task<AuthResult> IssueTokenPairAsync(User user)
    {
        var (rawRefreshToken, tokenHash) = tokenService.GenerateRefreshToken();
        var refreshTokenExpiresAt = tokenService.RefreshTokenExpiresAt();
        var accessTokenExpiresAt = tokenService.AccessTokenExpiresAt();

        var refreshToken = RefreshToken.CreateForLogin(
            userId: user.Id,
            tokenHash: tokenHash,
            expiresAt: refreshTokenExpiresAt
        );

        dbContext.RefreshTokens.Add(refreshToken);
        await dbContext.SaveChangesAsync();

        var accessToken = tokenService.GenerateAccessToken(user);
        return BuildResponse(user, accessToken, rawRefreshToken, refreshTokenExpiresAt, accessTokenExpiresAt);
    }

    private static AuthResult BuildResponse(
        User user,
        string accessToken,
        string rawRefreshToken,
        DateTimeOffset refreshTokenExpiresAt,
        DateTimeOffset accessTokenExpiresAt)
    {
        return new AuthResult(
            AccessToken: accessToken,
            RawRefreshToken: rawRefreshToken,
            RefreshTokenExpiresAt: refreshTokenExpiresAt,
            AccessTokenExpiresAt: accessTokenExpiresAt,
            User: new UserDto(
                Id: user.Id,
                Name: user.Name,
                Email: user.Email,
                Role: user.Role.ToString()
            )
        );
    }

    private async Task RevokeFamilyAsync(Guid familyId, RevokeReasons reason)
    {
        // Use ExecuteUpdateAsync to bypass change tracker issues and update directly in DB
        var now = DateTimeOffset.UtcNow;
        var revokedCount = await dbContext.RefreshTokens
            .Where(rt => rt.FamilyId == familyId && rt.RevokedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAt, now)
                .SetProperty(t => t.RevokedReason, reason)
            );

        // Clear change tracker to ensure subsequent reads (especially in tests)
        // reflect the direct DB update.
        dbContext.ChangeTracker.Clear();

        logger.LogWarning(
            "Token family revoked via ExecuteUpdate. FamilyId={FamilyId} Reason={Reason} Count={Count}",
            familyId, reason, revokedCount
        );
    }
}