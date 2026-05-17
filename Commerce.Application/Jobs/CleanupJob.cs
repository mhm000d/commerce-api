using Commerce.Application.Database;
using Commerce.Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Commerce.Application.Jobs;

/// <summary>
/// Hangfire recurring job — runs daily at 02:00 UTC.
/// Removes stale security tokens to keep the database lean.
///
/// Idempotent: purely additive deletes — safe to re-run or run late.
///
/// Scope of cleanup:
///   1. Expired PasswordResetTokens (past ExpiresAt, used or not)
///   2. Revoked RefreshTokens older than 30 days
///   3. PermanentlyFailed EmailNotifications older than 90 days
///      (keeps audit history while not growing forever)
/// </summary>
public class CleanupJob(
    AppDbContext dbContext,
    ILogger<CleanupJob> logger)
{
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        await CleanupPasswordResetTokensAsync(now, ct);
        await CleanupRefreshTokensAsync(now, ct);
        await CleanupEmailNotificationsAsync(now, ct);

        logger.LogInformation("CleanupJob: complete");
    }

    // ── Step 1: PasswordResetTokens ───────────────────────────────────────────

    private async Task CleanupPasswordResetTokensAsync(DateTimeOffset now, CancellationToken ct)
    {
        // Delete tokens that are past their expiry regardless of UsedAt.
        // A used + expired token has no further purpose.
        var deleted = await dbContext.PasswordResetTokens
            .Where(t => t.ExpiresAt < now)
            .ExecuteDeleteAsync(ct);

        if (deleted > 0)
            logger.LogInformation(
                "CleanupJob: deleted {Count} expired PasswordResetToken(s)", deleted);
    }

    // ── Step 2: RefreshTokens ─────────────────────────────────────────────────

    private async Task CleanupRefreshTokensAsync(DateTimeOffset now, CancellationToken ct)
    {
        var retentionCutoff = now.AddDays(-30);

        // Only delete revoked tokens — expired-but-not-revoked tokens
        // are handled implicitly (they fail IsExpired checks) but keeping
        // them a bit longer helps with reuse-attack forensics.
        var deleted = await dbContext.RefreshTokens
            .Where(t => t.RevokedAt != null
                        && t.RevokedAt < retentionCutoff)
            .ExecuteDeleteAsync(ct);

        if (deleted > 0)
            logger.LogInformation(
                "CleanupJob: deleted {Count} revoked RefreshToken(s) older than 30 days", deleted);
    }

    // ── Step 3: EmailNotifications ────────────────────────────────────────────

    private async Task CleanupEmailNotificationsAsync(DateTimeOffset now, CancellationToken ct)
    {
        var retentionCutoff = now.AddDays(-90);

        var deleted = await dbContext.EmailNotifications
            .Where(n => n.Status == EmailStatus.PermanentlyFailed
                        && n.CreatedAt < retentionCutoff)
            .ExecuteDeleteAsync(ct);

        if (deleted > 0)
            logger.LogInformation(
                "CleanupJob: deleted {Count} PermanentlyFailed EmailNotification(s) older than 90 days",
                deleted);
    }
}