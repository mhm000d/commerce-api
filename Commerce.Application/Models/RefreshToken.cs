namespace Commerce.Application.Models;

/// <summary>
/// Refresh token with token-family tracking and reuse detection support.
///
/// Chain example (FamilyId stays the same across the entire chain):
///   Login  → RT1 (FamilyId: F1, new family born)
///   Rotate → RT1 revoked (Rotated, ReplacedBy=RT2), RT2 issued (FamilyId: F1)
///   Rotate → RT2 revoked (Rotated, ReplacedBy=RT3), RT3 issued (FamilyId: F1)
///   Attack → RT1 replayed → entire F1 family revoked → all sessions killed
/// </summary>
public class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = null!;

    /// <summary>
    /// Groups all tokens born from the same login into one chain.
    /// When reuse is detected, we revoke every token sharing this FamilyId,
    /// instead of the nuclear option of revoking every token the user owns.
    /// </summary>
    public Guid FamilyId { get; private set; }

    /// <summary>Set during rotation — points to the token that replaced this one.</summary>
    public Guid? ReplacedByTokenId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public RevokeReasons? RevokedReason { get; private set; }

    // ── Navigation Properties ─────────────────────────────────────────────────
    public User User { get; private set; } = null!;


    // ── Computed ──────────────────────────────────────────────────────────────
    public bool IsActive => RevokedAt is null && DateTimeOffset.UtcNow < ExpiresAt;
    public bool IsRevoked => RevokedAt.HasValue;
    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;

    // ── Factories ─────────────────────────────────────────────────────────────

    /// <summary>Call at login — starts a brand-new family chain.</summary>
    public static RefreshToken CreateForLogin(
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAt)
        => new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            FamilyId = Guid.NewGuid(), // new chain born here
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt,
        };

    /// <summary>Call during rotation — inherits the parent's FamilyId.</summary>
    public static RefreshToken CreateRotated(
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        Guid familyId) // inherited, keeps the chain intact
        => new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            FamilyId = familyId,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt,
        };

    // ── Behaviours ────────────────────────────────────────────────────────────

    /// <summary>Called on the OLD token when it is successfully rotated.</summary>
    public void MarkRotated(Guid replacedByTokenId)
    {
        RevokedAt = DateTimeOffset.UtcNow;
        RevokedReason = RevokeReasons.Rotated;
        ReplacedByTokenId = replacedByTokenId;
    }

    /// <summary>General-purpose revocation (logout, password reset, theft).</summary>
    public void Revoke(RevokeReasons reason)
    {
        if (IsRevoked) return;
        RevokedAt = DateTimeOffset.UtcNow;
        RevokedReason = reason;
    }
}

public enum RevokeReasons
{
    Rotated,
    Logout,
    PasswordReset,
    ReuseDetected,
}