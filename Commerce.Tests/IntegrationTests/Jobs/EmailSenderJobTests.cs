using Commerce.Application.Exceptions;
using Commerce.Application.Jobs;
using Commerce.Application.Models;
using Commerce.Application.Services.Email;
using Commerce.Application.Services.Email.Templates;
using Commerce.Application.Settings;
using Commerce.Tests.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Commerce.Tests.IntegrationTests.Jobs;

[Collection("Database")]
public class EmailSenderJobTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private IEmailService _emailServiceMock = null!;
    private EmailSenderJob _job = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        _emailServiceMock = Substitute.For<IEmailService>();

        _job = new EmailSenderJob(
            dbContext:     DbContext,
            emailService:  _emailServiceMock,
            renderer:      new EmailTemplateRenderer(Options.Create(new EmailSettings
            {
                FromAddress     = "noreply@commerce.com",
                FromName        = "Commerce",
                FrontendBaseUrl = "https://app.commerce.com"
            })),
            logger: Substitute.For<ILogger<EmailSenderJob>>());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static EmailNotification MakePendingOrderNotification(
        string to = "customer@example.com",
        int maxAttempts = 3) =>
        EmailNotification.Create(
            recipientEmail: to,
            template: EmailTemplate.OrderConfirmation,
            templateData: new Dictionary<string, string>
            {
                ["CustomerName"] = "John",
                ["OrderNumber"]  = "Order #000000001",
                ["OrderId"]      = Guid.NewGuid().ToString(),
                ["TotalAmount"]  = "49.99",
                ["Items"]        = "[]"
            },
            maxAttempts: maxAttempts);

    private static EmailNotification MakePendingPasswordResetNotification(string to = "user@example.com") =>
        EmailNotification.Create(
            recipientEmail: to,
            template: EmailTemplate.PasswordReset,
            templateData: new Dictionary<string, string>
            {
                ["ResetUrl"]  = "https://app.commerce.com/reset-password?token=tok",
                ["ExpiresIn"] = "1 hour"
            });

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WhenPendingNotification_ShouldSendEmailAndMarkSent()
    {
        var notification = MakePendingOrderNotification();
        await SaveAsync(notification);

        await _job.ExecuteAsync();

        var updated = await DbContext.EmailNotifications.FindAsync(notification.Id);
        updated!.Status.ShouldBe(EmailStatus.Sent);
        updated.Attempts.ShouldBe(1);
        updated.SentAt.ShouldNotBeNull();

        await _emailServiceMock.Received(1)
            .SendAsync("customer@example.com", Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenFailedNotificationWithAttemptsRemaining_ShouldRetryAndMarkSent()
    {
        var notification = MakePendingOrderNotification(maxAttempts: 3);
        await SaveAsync(notification);

        // Simulate one prior failed attempt stored in DB
        await DbContext.EmailNotifications
            .Where(n => n.Id == notification.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.Status,   EmailStatus.Failed)
                .SetProperty(n => n.Attempts, 1));

        await _job.ExecuteAsync();

        var updated = await DbContext.EmailNotifications.FindAsync(notification.Id);
        updated!.Status.ShouldBe(EmailStatus.Sent);
        updated.Attempts.ShouldBe(2);
    }

    // ── Skipping ineligible notifications ─────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WhenNotificationAlreadySent_ShouldSkipIt()
    {
        var notification = MakePendingOrderNotification();
        await SaveAsync(notification);
        await DbContext.EmailNotifications
            .Where(n => n.Id == notification.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.Status, EmailStatus.Sent));

        await _job.ExecuteAsync();

        await _emailServiceMock.DidNotReceive()
            .SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenNotificationPermanentlyFailed_ShouldSkipIt()
    {
        var notification = MakePendingOrderNotification(maxAttempts: 1);
        notification.ForceExhaustAttempts();
        notification.RecordAttempt(success: false, errorMessage: "Hard fail");
        await SaveAsync(notification);

        await _job.ExecuteAsync();

        await _emailServiceMock.DidNotReceive()
            .SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoNotifications_ShouldNotCallEmailService()
    {
        await _job.ExecuteAsync();

        await _emailServiceMock.DidNotReceive()
            .SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    // ── Failure handling ──────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WhenTransientFailure_ShouldIncrementAttemptsAndSetFailed()
    {
        _emailServiceMock
            .SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException(new Exception("Network timeout")));

        var notification = MakePendingOrderNotification(maxAttempts: 3);
        await SaveAsync(notification);

        await _job.ExecuteAsync();

        var updated = await DbContext.EmailNotifications.FindAsync(notification.Id);
        updated!.Status.ShouldBe(EmailStatus.Failed);
        updated.Attempts.ShouldBe(1);
        updated.ErrorMessage.ShouldBe("Network timeout");
    }

    [Fact]
    public async Task ExecuteAsync_WhenFinalTransientFailure_ShouldSetPermanentlyFailed()
    {
        _emailServiceMock
            .SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException(new Exception("Still failing")));

        // maxAttempts: 1 → first failure exhausts the budget
        var notification = MakePendingOrderNotification(maxAttempts: 1);
        await SaveAsync(notification);

        await _job.ExecuteAsync();

        var updated = await DbContext.EmailNotifications.FindAsync(notification.Id);
        updated!.Status.ShouldBe(EmailStatus.PermanentlyFailed);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPermanentEmailException_ShouldSetPermanentlyFailedWithoutExhaustingRetries()
    {
        // EmailPermanentException skips the retry budget — immediately PermanentlyFailed
        _emailServiceMock
            .SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException(
                new EmailPermanentException("SES: SendingPaused")));

        var notification = MakePendingOrderNotification(maxAttempts: 3);
        await SaveAsync(notification);

        await _job.ExecuteAsync();

        var updated = await DbContext.EmailNotifications.FindAsync(notification.Id);
        updated!.Status.ShouldBe(EmailStatus.PermanentlyFailed);

        // The email service was only called once — no retry loop
        await _emailServiceMock.Received(1)
            .SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<CancellationToken>());
    }

    // ── Batch resilience ──────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WhenOneFails_ShouldContinueAndProcessOthers()
    {
        var failing  = MakePendingOrderNotification(to: "fails@example.com");
        var succeeds = MakePendingPasswordResetNotification(to: "ok@example.com");
        await SaveAsync(failing, succeeds);

        _emailServiceMock
            .SendAsync("fails@example.com", Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException(new Exception("Only this one fails")));

        await _job.ExecuteAsync();

        var failedResult   = await DbContext.EmailNotifications.FindAsync(failing.Id);
        var succeededResult = await DbContext.EmailNotifications.FindAsync(succeeds.Id);

        failedResult!.Status.ShouldBe(EmailStatus.Failed);
        succeededResult!.Status.ShouldBe(EmailStatus.Sent);
    }
}