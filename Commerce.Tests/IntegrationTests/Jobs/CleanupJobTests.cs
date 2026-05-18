using Commerce.Application.Jobs;
using Commerce.Application.Models;
using Commerce.Tests.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Commerce.Tests.IntegrationTests.Jobs;

[Collection("Database")]
public class CleanupJobTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private CleanupJob _job = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _job = new CleanupJob(DbContext, Substitute.For<ILogger<CleanupJob>>());
    }

    // ── Arrange helpers ───────────────────────────────────────────────────────

    private async Task<User> SeedUserAsync()
    {
        var user = User.Create("Test", $"{Guid.NewGuid()}@example.com", "Password1", null);
        await SaveAsync(user);
        return user;
    }

    private async Task<PasswordResetToken> SeedPasswordResetTokenAsync(
        Guid userId, DateTimeOffset expiresAt)
    {
        var tokenHash = Guid.NewGuid().ToString("N");
        var token     = PasswordResetToken.Create(userId, tokenHash);
        await SaveAsync(token);

        await DbContext.PasswordResetTokens
            .Where(t => t.Id == token.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.ExpiresAt, expiresAt));

        return token;
    }

    private async Task<RefreshToken> SeedRevokedRefreshTokenAsync(
        Guid userId, DateTimeOffset revokedAt)
    {
        var tokenHash = Guid.NewGuid().ToString("N");
        var token     = RefreshToken.CreateForLogin(
            userId, tokenHash, DateTimeOffset.UtcNow.AddDays(7));
        token.Revoke(RevokeReasons.Logout);
        await SaveAsync(token);

        await DbContext.RefreshTokens
            .Where(t => t.Id == token.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, revokedAt));

        return token;
    }

    private async Task<RefreshToken> SeedActiveRefreshTokenAsync(Guid userId)
    {
        var tokenHash = Guid.NewGuid().ToString("N");
        var token     = RefreshToken.CreateForLogin(
            userId, tokenHash, DateTimeOffset.UtcNow.AddDays(7));
        await SaveAsync(token);
        return token;
    }

    private async Task<EmailNotification> SeedEmailNotificationAsync(
        EmailStatus status, DateTimeOffset createdAt)
    {
        var notification = EmailNotification.Create(
            recipientEmail: "test@example.com",
            template:       EmailTemplate.OrderConfirmation,
            templateData:   new Dictionary<string, string> { ["OrderNumber"] = "000000001" });

        if (status == EmailStatus.PermanentlyFailed)
        {
            notification.ForceExhaustAttempts();
            notification.RecordAttempt(success: false, errorMessage: "Permanent failure");
        }

        await SaveAsync(notification);

        await DbContext.EmailNotifications
            .Where(n => n.Id == notification.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.CreatedAt, createdAt));

        return notification;
    }

    // ── PasswordResetToken cleanup ────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_ShouldDeleteExpiredPasswordResetTokens()
    {
        var user    = await SeedUserAsync();
        var expired = await SeedPasswordResetTokenAsync(
            user.Id, DateTimeOffset.UtcNow.AddHours(-2));

        await _job.ExecuteAsync();

        var exists = await DbContext.PasswordResetTokens.AnyAsync(t => t.Id == expired.Id);
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotDeleteValidPasswordResetTokens()
    {
        var user  = await SeedUserAsync();
        var valid = await SeedPasswordResetTokenAsync(
            user.Id, DateTimeOffset.UtcNow.AddMinutes(30));

        await _job.ExecuteAsync();

        var exists = await DbContext.PasswordResetTokens.AnyAsync(t => t.Id == valid.Id);
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldDeleteExpiredTokens_ButKeepValidOnesInSameRun()
    {
        var user    = await SeedUserAsync();
        var expired = await SeedPasswordResetTokenAsync(user.Id, DateTimeOffset.UtcNow.AddHours(-3));
        var valid   = await SeedPasswordResetTokenAsync(user.Id, DateTimeOffset.UtcNow.AddHours(1));

        await _job.ExecuteAsync();

        (await DbContext.PasswordResetTokens.AnyAsync(t => t.Id == expired.Id)).ShouldBeFalse();
        (await DbContext.PasswordResetTokens.AnyAsync(t => t.Id == valid.Id)).ShouldBeTrue();
    }

    // ── RefreshToken cleanup ──────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_ShouldDeleteRevokedRefreshTokensOlderThan30Days()
    {
        var user = await SeedUserAsync();
        var old  = await SeedRevokedRefreshTokenAsync(
            user.Id, DateTimeOffset.UtcNow.AddDays(-31));

        await _job.ExecuteAsync();

        var exists = await DbContext.RefreshTokens.AnyAsync(t => t.Id == old.Id);
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldKeepRecentlyRevokedRefreshTokens()
    {
        var user   = await SeedUserAsync();
        var recent = await SeedRevokedRefreshTokenAsync(
            user.Id, DateTimeOffset.UtcNow.AddDays(-5));

        await _job.ExecuteAsync();

        var exists = await DbContext.RefreshTokens.AnyAsync(t => t.Id == recent.Id);
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNeverDeleteActiveRefreshTokens()
    {
        var user   = await SeedUserAsync();
        var active = await SeedActiveRefreshTokenAsync(user.Id);

        await _job.ExecuteAsync();

        var exists = await DbContext.RefreshTokens.AnyAsync(t => t.Id == active.Id);
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldDeleteOldRevokedTokens_ButKeepRecentOnesInSameRun()
    {
        var user   = await SeedUserAsync();
        var old    = await SeedRevokedRefreshTokenAsync(user.Id, DateTimeOffset.UtcNow.AddDays(-31));
        var recent = await SeedRevokedRefreshTokenAsync(user.Id, DateTimeOffset.UtcNow.AddDays(-10));

        await _job.ExecuteAsync();

        (await DbContext.RefreshTokens.AnyAsync(t => t.Id == old.Id)).ShouldBeFalse();
        (await DbContext.RefreshTokens.AnyAsync(t => t.Id == recent.Id)).ShouldBeTrue();
    }

    // ── EmailNotification cleanup ─────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_ShouldDeletePermanentlyFailedNotificationsOlderThan90Days()
    {
        var old = await SeedEmailNotificationAsync(
            EmailStatus.PermanentlyFailed, DateTimeOffset.UtcNow.AddDays(-91));

        await _job.ExecuteAsync();

        var exists = await DbContext.EmailNotifications.AnyAsync(n => n.Id == old.Id);
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldKeepRecentPermanentlyFailedNotifications()
    {
        var recent = await SeedEmailNotificationAsync(
            EmailStatus.PermanentlyFailed, DateTimeOffset.UtcNow.AddDays(-30));

        await _job.ExecuteAsync();

        var exists = await DbContext.EmailNotifications.AnyAsync(n => n.Id == recent.Id);
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNeverDeletePendingNotificationsRegardlessOfAge()
    {
        // Even a very old PENDING notification must not be deleted —
        // the job only cleans PermanentlyFailed
        var oldPending = await SeedEmailNotificationAsync(
            EmailStatus.Pending, DateTimeOffset.UtcNow.AddDays(-200));

        await _job.ExecuteAsync();

        var exists = await DbContext.EmailNotifications.AnyAsync(n => n.Id == oldPending.Id);
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNeverDeleteFailedNotificationsRegardlessOfAge()
    {
        var oldFailed = await SeedEmailNotificationAsync(
            EmailStatus.Failed, DateTimeOffset.UtcNow.AddDays(-200));

        await _job.ExecuteAsync();

        var exists = await DbContext.EmailNotifications.AnyAsync(n => n.Id == oldFailed.Id);
        exists.ShouldBeTrue();
    }

    // ── Idempotency ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_RunTwiceOnSameData_ShouldNotThrow()
    {
        var user = await SeedUserAsync();
        await SeedPasswordResetTokenAsync(user.Id, DateTimeOffset.UtcNow.AddHours(-2));
        await SeedRevokedRefreshTokenAsync(user.Id, DateTimeOffset.UtcNow.AddDays(-31));
        await SeedEmailNotificationAsync(
            EmailStatus.PermanentlyFailed, DateTimeOffset.UtcNow.AddDays(-91));

        await _job.ExecuteAsync();
        await Should.NotThrowAsync(() => _job.ExecuteAsync()); // second run hits empty tables
    }
}
