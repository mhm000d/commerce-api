using Amazon.Runtime;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using Commerce.Application.Exceptions;
using Commerce.Application.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Commerce.Application.Services.Email;

public class SesEmailService(
    IAmazonSimpleEmailServiceV2 sesClient,
    IOptions<EmailSettings> settings,
    ILogger<SesEmailService> logger) : IEmailService
{
    private readonly EmailSettings _settings = settings.Value; 
    
    // Hard-failure error codes — retrying these will never succeed.
    // Full list: https://docs.aws.amazon.com/ses/latest/APIReference-V2/API_SendEmail.html
    private static readonly HashSet<string> PermanentErrorCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "AccountSuspended",         // AWS suspended the account
        "SendingPaused",            // Sending paused via SES console
        "MessageRejected",          // SES rejected the message (content policy)
        "MailFromDomainNotVerified",
        "InvalidClientTokenId",     // Wrong AWS credentials — retrying won't help
        "AccessDenied",
    };
    
    public async Task SendAsync(
        string toAddress,
        string subject,
        string htmlBody,
        CancellationToken ct = default)
    {
        var request = new SendEmailRequest
        {
            FromEmailAddress = $"{_settings.FromName} <{_settings.FromAddress}>",
            Destination = new Destination
            {
                ToAddresses = [toAddress]
            },
            Content = new EmailContent
            {
                Simple = new Message
                {
                    Subject = new Content { Data = subject, Charset = "UTF-8" },
                    Body = new Body
                    {
                        Html = new Content { Data = htmlBody, Charset = "UTF-8" }
                    }
                }
            },
            // Prevents SES from tracking opens/clicks — GDPR-friendly for MVP
            ConfigurationSetName = null
        };
        
        try
        {
            var response = await sesClient.SendEmailAsync(request, ct);

            logger.LogInformation(
                "Email sent via SES. To={To} Subject={Subject} MessageId={MessageId}",
                toAddress, subject, response.MessageId);
        }
        catch (AmazonSimpleEmailServiceV2Exception ex)
            when (PermanentErrorCodes.Contains(ex.ErrorCode))
        {
            // Hard failure — mark as PermanentlyFailed immediately,
            // no point in the job retrying this.
            logger.LogError(ex,
                "SES permanent failure [{ErrorCode}]. To={To} Subject={Subject}",
                ex.ErrorCode, toAddress, subject);

            throw new EmailPermanentException(
                $"SES rejected email permanently: {ex.ErrorCode}", ex);
        }
        catch (AmazonSimpleEmailServiceV2Exception ex)
        {
            // Transient — throttle, network blip, etc.
            // EmailSenderJob will retry up to MaxAttempts.
            logger.LogWarning(ex,
                "SES transient failure [{ErrorCode}] (will retry). To={To}",
                ex.ErrorCode, toAddress);
            throw;
        }
        catch (AmazonServiceException ex)
        {
            // AWS-level networking / auth issues — transient
            logger.LogWarning(ex,
                "AWS service error sending email (will retry). To={To}", toAddress);
            throw;
        }
    }
}