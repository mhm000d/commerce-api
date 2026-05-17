namespace Commerce.Application.Settings;

public class EmailSettings
{
    public const string SectionName = "Email";

    /// <summary>Verified sender address in AWS SES (e.g., noreply@yourdomain.com)</summary>
    public string FromAddress { get; init; } = null!;

    /// <summary>Display name shown in email clients (e.g. "Commerce Store")</summary>
    public string FromName { get; init; } = null!;

    /// <summary>Frontend base URL used to build links in emails</summary>
    public string FrontendBaseUrl { get; init; } = null!;
}