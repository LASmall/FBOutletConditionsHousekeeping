using Microsoft.Extensions.Options;

namespace FBOutletConditionsHousekeeping.Core.Configuration;

/// <summary>
/// Enforces VAL-002 (docs/SPECIFICATION.md): when EmailEnabled is true,
/// EmailRecipients must contain at least one syntactically valid email
/// address, and EmailFromAddress must itself be valid.
/// </summary>
public sealed class EmailOptionsValidator : IValidateOptions<EmailOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailOptions options)
    {
        if (!options.EmailEnabled)
        {
            // Nothing further to validate when notifications are disabled.
            return ValidateOptionsResult.Success;
        }

        var recipients = options.GetRecipients();
        if (recipients.Count == 0 || !recipients.All(IsValidEmail))
        {
            return ValidateOptionsResult.Fail(
                "EmailRecipients must contain at least one valid email address when EmailEnabled is true.");
        }

        if (string.IsNullOrWhiteSpace(options.EmailFromAddress) || !IsValidEmail(options.EmailFromAddress))
        {
            return ValidateOptionsResult.Fail(
                "EmailFromAddress must be a valid email address when EmailEnabled is true.");
        }

        if (string.IsNullOrWhiteSpace(options.SmtpHost))
        {
            return ValidateOptionsResult.Fail("SmtpHost must be configured when EmailEnabled is true.");
        }

        return ValidateOptionsResult.Success;
    }

    private static bool IsValidEmail(string candidate) =>
        System.Net.Mail.MailAddress.TryCreate(candidate, out _);
}
