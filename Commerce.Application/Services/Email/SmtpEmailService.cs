using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace Commerce.Application.Services.Email;

public class SmtpEmailService(IConfiguration configuration) : IEmailService
{
    public async Task SendAsync(
        string toAddress,
        string subject,
        string htmlBody,
        CancellationToken ct = default)
    {
        var host = configuration["Smtp:Host"]
                   ?? throw new InvalidOperationException("SMTP host is not configured.");

        var portValue = configuration["Smtp:Port"]
                        ?? throw new InvalidOperationException("SMTP port is not configured.");

        var fromAddress = configuration["Email:FromAddress"]
                          ?? throw new InvalidOperationException("Email from address is not configured.");

        var fromName = configuration["Email:FromName"] ?? "Commerce Store";

        var username = configuration["Smtp:Username"];
        var password = configuration["Smtp:Password"];

        var useSsl = bool.TryParse(configuration["Smtp:UseSsl"], out var ssl) && ssl;

        var message = new MimeMessage();

        message.From.Add(new MailboxAddress(fromName, fromAddress));
        message.To.Add(MailboxAddress.Parse(toAddress));
        message.Subject = subject;

        message.Body = new BodyBuilder
        {
            HtmlBody = htmlBody
        }.ToMessageBody();

        using var client = new SmtpClient();

        var socketOptions = useSsl
            ? SecureSocketOptions.StartTls
            : SecureSocketOptions.None;

        await client.ConnectAsync(host, int.Parse(portValue), socketOptions, ct);

        if (!string.IsNullOrWhiteSpace(username))
        {
            await client.AuthenticateAsync(username, password, ct);
        }

        await client.SendAsync(message, ct);

        await client.DisconnectAsync(true, ct);
    }
}