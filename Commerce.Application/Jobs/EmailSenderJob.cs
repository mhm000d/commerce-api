using Commerce.Application.Database;
using Commerce.Application.Exceptions;
using Commerce.Application.Models;
using Commerce.Application.Services.Email;
using Commerce.Application.Services.Email.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Commerce.Application.Jobs;

/// <summary>
/// Hangfire recurring job — runs every minute.
/// Picks up PENDING and FAILED notifications and attempts delivery via IEmailService.
/// Idempotent: concurrent runs are safe because Hangfire uses distributed locks.
/// </summary>
public class EmailSenderJob(
    AppDbContext dbContext,
    IEmailService emailService,
    EmailTemplateRenderer renderer,
    ILogger<EmailSenderJob> logger)
{
    // Called by Hangfire — registered in DI as transient.
    public async Task ExecuteAsync()
    {
        // Fetch batch of eligible notifications.
        // PermanentlyFailed and Sent are intentionally excluded.
        var pending = await dbContext.EmailNotifications
            .Where(n => n.Status == EmailStatus.Pending || n.Status == EmailStatus.Failed)
            .OrderBy(n => n.CreatedAt)
            .Take(50) // process max 50 per run to keep job fast
            .ToListAsync();

        if (pending.Count == 0)
            return;

        logger.LogInformation("EmailSenderJob: processing {Count} notifications", pending.Count);

        foreach (var notification in pending)
        {
            await TrySendAsync(notification);
        }

        await dbContext.SaveChangesAsync();

        logger.LogInformation("EmailSenderJob: batch complete");
    }

    private async Task TrySendAsync(EmailNotification notification)
    {
        try
        {
            var (subject, htmlBody) = renderer.Render(
                notification.Template,
                notification.TemplateData);

            await emailService.SendAsync(
                toAddress: notification.RecipientEmail,
                subject: subject,
                htmlBody: htmlBody);

            notification.RecordAttempt(success: true);
            await TryMarkOrderConfirmationSentAsync(notification);

            logger.LogInformation(
                "Email sent. NotificationId={Id} Template={Template} To={To}",
                notification.Id, notification.Template, notification.RecipientEmail);
        }
        catch (EmailPermanentException ex)
        {
            // Skip remaining retries — exhaust attempts immediately.
            // Force Attempts to MaxAttempts so RecordAttempt sets PermanentlyFailed.
            notification.ForceExhaustAttempts();
            notification.RecordAttempt(success: false, errorMessage: ex.Message);

            logger.LogError(
                "Email permanently failed (not retriable). NotificationId={Id} To={To} Reason={Reason}",
                notification.Id, notification.RecipientEmail, ex.Message);
        }
        catch (Exception ex)
        {
            notification.RecordAttempt(success: false, errorMessage: ex.Message);

            logger.LogWarning(
                "Email attempt failed. NotificationId={Id} Attempts={Attempts}/{Max} Error={Error}",
                notification.Id, notification.Attempts, notification.MaxAttempts, ex.Message);

            if (notification.Status == EmailStatus.PermanentlyFailed)
            {
                logger.LogError(
                    "Email permanently failed after {Max} attempts. NotificationId={Id} To={To}",
                    notification.MaxAttempts, notification.Id, notification.RecipientEmail);
            }
        }
    }

    private async Task TryMarkOrderConfirmationSentAsync(EmailNotification notification)
    {
        if (notification.Template != EmailTemplate.OrderConfirmation || !notification.OrderId.HasValue)
            return;

        try
        {
            var order = await dbContext.Orders.FindAsync(notification.OrderId.Value);
            order?.MarkConfirmationEmailSent();
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to mark order confirmation email as sent. OrderId={OrderId} NotificationId={NotificationId}",
                notification.OrderId,
                notification.Id);
        }
    }
}
