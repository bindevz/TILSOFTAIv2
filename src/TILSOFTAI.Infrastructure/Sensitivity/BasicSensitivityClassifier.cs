using System.Text.RegularExpressions;
using TILSOFTAI.Domain.Sensitivity;

namespace TILSOFTAI.Infrastructure.Sensitivity;

/// <summary>
/// Basic deterministic sensitivity classifier using regex patterns and keyword matching.
/// Reuses patterns from BasicLogRedactor and adds keyword detection for common sensitive terms.
/// </summary>
public sealed class BasicSensitivityClassifier : ISensitivityClassifier
{
    // Regex patterns (reused from BasicLogRedactor)
    private static readonly Regex EmailRegex = new Regex(
        @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PhoneRegex = new Regex(
        @"\b\d{3}[-.]?\d{3}[-.]?\d{4}\b",
        RegexOptions.Compiled);

    private static readonly Regex CreditCardRegex = new Regex(
        @"\b\d{4}[-\s]?\d{4}[-\s]?\d{4}[-\s]?\d{4}\b",
        RegexOptions.Compiled);

    private static readonly Regex LongHexIdRegex = new Regex(
        @"\b[0-9a-f]{32,}\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex SsnRegex = new Regex(
        @"\b\d{3}-\d{2}-\d{4}\b",
        RegexOptions.Compiled);

    // Sensitive keywords
    private static readonly string[] SensitiveKeywords =
    {
        "password", "passwd", "pwd",
        "token", "bearer", "jwt",
        "secret", "api_key", "apikey", "api-key",
        "ssn", "social security",
        "credit card", "creditcard",
        "bank account", "routing number",
        "private key", "privatekey",
        "access_token", "refresh_token",
        "client_secret", "client secret"
    };

    public SensitivityResult Classify(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new SensitivityResult
            {
                ContainsSensitive = false,
                Reasons = Array.Empty<string>()
            };
        }

        var reasons = new List<string>();

        // Check for email addresses
        if (EmailRegex.IsMatch(text))
        {
            reasons.Add("Contains email address");
        }

        // Check for phone numbers
        if (PhoneRegex.IsMatch(text))
        {
            reasons.Add("Contains phone number");
        }

        // Check for credit card patterns
        if (CreditCardRegex.IsMatch(text))
        {
            reasons.Add("Contains credit card pattern");
        }

        // Check for long hex IDs (potential API keys, tokens)
        if (LongHexIdRegex.IsMatch(text))
        {
            reasons.Add("Contains potential API key or token (long hex string)");
        }

        // Check for SSN patterns
        if (SsnRegex.IsMatch(text))
        {
            reasons.Add("Contains social security number pattern");
        }

        // Check for sensitive keywords
        var lowerText = text.ToLowerInvariant();
        foreach (var keyword in SensitiveKeywords)
        {
            if (lowerText.Contains(keyword))
            {
                reasons.Add($"Contains sensitive keyword: '{keyword}'");
            }
        }

        return new SensitivityResult
        {
            ContainsSensitive = reasons.Count > 0,
            Reasons = reasons
        };
    }
}
