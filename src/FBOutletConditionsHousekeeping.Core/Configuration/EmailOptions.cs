namespace FBOutletConditionsHousekeeping.Core.Configuration;

/// <summary>
/// Controls whether, and how, cleanup summary/failure notification emails
/// are sent via SMTP (MailKit).
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>
    /// Controls whether the cleanup summary/failure emails are sent at all.
    /// Defaults to false so a fresh deployment does not send email until
    /// explicitly configured and enabled.
    /// </summary>
    public bool EmailEnabled { get; set; }

    /// <summary>
    /// Comma-separated list of email addresses that receive the cleanup
    /// summary/failure emails.
    /// </summary>
    public string EmailRecipients { get; set; } = string.Empty;

    /// <summary>
    /// The "From" address used when sending the cleanup emails.
    /// </summary>
    public string EmailFromAddress { get; set; } = string.Empty;

    /// <summary>SMTP server hostname.</summary>
    public string SmtpHost { get; set; } = string.Empty;

    /// <summary>SMTP server port.</summary>
    public int SmtpPort { get; set; } = 587;

    /// <summary>
    /// Whether to connect using implicit SSL. When false, MailKit negotiates
    /// STARTTLS on the given port (the common configuration for port 587).
    /// </summary>
    public bool SmtpUseSsl { get; set; }

    /// <summary>SMTP authentication username.</summary>
    public string SmtpUsername { get; set; } = string.Empty;

    /// <summary>SMTP authentication password.</summary>
    public string SmtpPassword { get; set; } = string.Empty;

    /// <summary>
    /// Parses <see cref="EmailRecipients"/> into individual, trimmed,
    /// non-empty addresses.
    /// </summary>
    public IReadOnlyList<string> GetRecipients() =>
        EmailRecipients
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
}
