using Commerce.Application.Models;
using Commerce.Application.Exceptions;
using Shouldly;

namespace Commerce.Tests.UnitTests.Models;

public class EmailNotificationModelTests
{
    private static EmailNotification CreateNotification(int maxAttempts = 3) =>
        EmailNotification.Create(
            recipientEmail: "test@example.com",
            template: EmailTemplate.OrderConfirmation,
            templateData: new Dictionary<string, string> { ["OrderNumber"] = "Order #1" },
            maxAttempts: maxAttempts);

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_ShouldInitializeWithPendingStatusAndZeroAttempts()
    {
        var notification = CreateNotification();

        notification.Status.ShouldBe(EmailStatus.Pending);
        notification.Attempts.ShouldBe(0);
        notification.SentAt.ShouldBeNull();
        notification.ErrorMessage.ShouldBeNull();
        notification.LastAttemptAt.ShouldBeNull();
    }

    // ── RecordAttempt — success ───────────────────────────────────────────────

    [Fact]
    public void RecordAttempt_WhenSuccess_ShouldSetStatusSentAndPopulateSentAt()
    {
        var notification = CreateNotification();

        notification.RecordAttempt(success: true);

        notification.Status.ShouldBe(EmailStatus.Sent);
        notification.SentAt.ShouldNotBeNull();
        notification.Attempts.ShouldBe(1);
        notification.LastAttemptAt.ShouldNotBeNull();
    }

    // ── RecordAttempt — failure ───────────────────────────────────────────────

    [Fact]
    public void RecordAttempt_WhenFirstFailure_ShouldSetStatusFailedAndStoreError()
    {
        var notification = CreateNotification(maxAttempts: 3);

        notification.RecordAttempt(success: false, errorMessage: "Network timeout");

        notification.Status.ShouldBe(EmailStatus.Failed);
        notification.Attempts.ShouldBe(1);
        notification.ErrorMessage.ShouldBe("Network timeout");
    }

    [Fact]
    public void RecordAttempt_WhenAttemptsReachMax_ShouldSetPermanentlyFailed()
    {
        var notification = CreateNotification(maxAttempts: 3);

        notification.RecordAttempt(success: false);
        notification.RecordAttempt(success: false);
        notification.RecordAttempt(success: false);

        notification.Status.ShouldBe(EmailStatus.PermanentlyFailed);
        notification.Attempts.ShouldBe(3);
    }

    [Fact]
    public void RecordAttempt_BeforeMaxAttempts_ShouldRemainFailed()
    {
        var notification = CreateNotification(maxAttempts: 3);

        notification.RecordAttempt(success: false);
        notification.RecordAttempt(success: false);

        // Still one attempt remaining
        notification.Status.ShouldBe(EmailStatus.Failed);
    }

    // ── ForceExhaustAttempts ──────────────────────────────────────────────────

    [Fact]
    public void ForceExhaustAttempts_ThenOneFailure_ShouldImmediatelySetPermanentlyFailed()
    {
        var notification = CreateNotification(maxAttempts: 3);

        notification.ForceExhaustAttempts();
        notification.RecordAttempt(success: false, errorMessage: "SES account suspended");

        notification.Status.ShouldBe(EmailStatus.PermanentlyFailed);
        // ForceExhaust sets Attempts to MaxAttempts-1, RecordAttempt increments once more
        notification.Attempts.ShouldBe(3);
    }

    [Fact]
    public void ForceExhaustAttempts_ShouldNotChangeStatus()
    {
        var notification = CreateNotification(maxAttempts: 3);

        notification.ForceExhaustAttempts();

        // Status is unchanged until RecordAttempt is called
        notification.Status.ShouldBe(EmailStatus.Pending);
    }
}