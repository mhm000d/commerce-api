namespace Commerce.Application.Models;

public class PasswordResetToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? UsedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    // ── Navigation Properties ─────────────────────────────────────────────────
    public User User { get; private set; } = null!;

    // ── Computed ──────────────────────────────────────────────────────────────
    public bool IsValid => UsedAt is null && DateTimeOffset.UtcNow < ExpiresAt;

    // ── Factory ───────────────────────────────────────────────────────────────
    public static PasswordResetToken Create(Guid userId, string tokenHash, int expiryMinutes = 60)
    {
        return new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes),
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    // ── Behaviour ─────────────────────────────────────────────────────────────
    public void MarkUsed() => UsedAt = DateTimeOffset.UtcNow;
}