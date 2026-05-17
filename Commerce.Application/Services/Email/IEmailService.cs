namespace Commerce.Application.Services.Email;

public interface IEmailService
{
    /// <summary>
    /// Sends a single transactional email. Throws on hard failure.
    /// Caller (EmailSenderJob) is responsible for retry logic.
    /// </summary>
    Task SendAsync(
        string toAddress,
        string subject,
        string htmlBody,
        CancellationToken ct = default);
}