using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TILSOFTAI.Domain.Validation;

namespace TILSOFTAI.Infrastructure.Validation;

/// <summary>
/// Detects common prompt injection patterns in user input.
/// </summary>
public sealed class PromptInjectionDetector
{
    private readonly ILogger<PromptInjectionDetector> _logger;

    // High severity patterns - clear injection attempts
    private static readonly Regex[] HighSeverityPatterns = new[]
    {
        new Regex(@"(?i)\bignore\s+(all\s+)?(previous|prior|above)\s+(instructions?|prompts?|rules?|guidelines?)", RegexOptions.Compiled),
        new Regex(@"(?i)\bdisregard\s+(all\s+)?(previous|prior|above|your)\s+(instructions?|prompts?|rules?|guidelines?)", RegexOptions.Compiled),
        new Regex(@"(?i)\bforget\s+(all\s+)?(previous|prior|everything|your)\s+(instructions?|prompts?|rules?)?", RegexOptions.Compiled),
        new Regex(@"(?i)\boverride\s+(all\s+)?(previous|prior|system|your)\s+(instructions?|prompts?|rules?)?", RegexOptions.Compiled),
        new Regex(@"(?i)^system\s*prompt\s*:", RegexOptions.Compiled | RegexOptions.Multiline),
        new Regex(@"(?i)^###\s*system\s*:", RegexOptions.Compiled | RegexOptions.Multiline),
        new Regex(@"(?i)\bnew\s+instructions?\s*:", RegexOptions.Compiled),
        new Regex(@"(?i)\byou\s+are\s+now\s+(a|an|my|the)\s+", RegexOptions.Compiled),
        new Regex(@"(?i)\bact\s+as\s+(if\s+)?(a|an|my|the)\s+", RegexOptions.Compiled),
        new Regex(@"(?i)\bpretend\s+(to\s+be|you\s+are)\s+", RegexOptions.Compiled),
    };

    // Medium severity patterns - suspicious but could be legitimate
    private static readonly Regex[] MediumSeverityPatterns = new[]
    {
        new Regex(@"(?i)\brole\s*:\s*(system|assistant|user)", RegexOptions.Compiled),
        new Regex(@"(?i)\b(jailbreak|escape|bypass)\b", RegexOptions.Compiled),
        new Regex(@"(?i)\bdo\s+not\s+follow\s+(your|the)\s+", RegexOptions.Compiled),
        new Regex(@"(?i)\b(DAN|STAN|DUDE|Developer\s+Mode)\b", RegexOptions.Compiled),
        new Regex(@"(?i)\bunrestricted\s+mode\b", RegexOptions.Compiled),
        new Regex(@"(?i)\bno\s+(restrictions?|limitations?|filters?|rules?)\b", RegexOptions.Compiled),
        new Regex(@"(?i)\brespond\s+without\s+(restrictions?|limitations?)\b", RegexOptions.Compiled),
        new Regex(@"(?i)\bsudo\s+mode\b", RegexOptions.Compiled),
        new Regex(@"(?i)^<\|im_(start|end)\|>", RegexOptions.Compiled | RegexOptions.Multiline),
        new Regex(@"(?i)^<\|(system|user|assistant)\|>", RegexOptions.Compiled | RegexOptions.Multiline),
    };

    // Low severity patterns - roleplay indicators that might be innocent
    private static readonly Regex[] LowSeverityPatterns = new[]
    {
        new Regex(@"(?i)\bfrom\s+now\s+on\s+(you\s+)?(will|are|must)\b", RegexOptions.Compiled),
        new Regex(@"(?i)\bimagine\s+you\s+are\b", RegexOptions.Compiled),
        new Regex(@"(?i)\bfor\s+this\s+conversation\s*,?\s*(you|act)\b", RegexOptions.Compiled),
        new Regex(@"(?i)\bplease\s+roleplay\b", RegexOptions.Compiled),
        new Regex(@"(?i)\blet'?s?\s+play\s+a\s+(game|role)\b", RegexOptions.Compiled),
    };

    // Base64 encoded instruction detection
    private static readonly Regex Base64Pattern = new(@"(?i)(base64|aWdub3Jl|c3lzdGVt|b3ZlcnJpZGU)", RegexOptions.Compiled);

    public PromptInjectionDetector(ILogger<PromptInjectionDetector> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Analyzes input for prompt injection patterns.
    /// </summary>
    /// <param name="input">The input to analyze.</param>
    /// <returns>The detected severity level.</returns>
    public PromptInjectionSeverity Detect(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return PromptInjectionSeverity.None;
        }

        // Check high severity patterns first
        foreach (var pattern in HighSeverityPatterns)
        {
            if (pattern.IsMatch(input))
            {
                _logger.LogWarning(
                    "High severity prompt injection pattern detected. Pattern: {Pattern}, InputSample: {Sample}",
                    pattern.ToString(),
                    TruncateForLog(input));
                return PromptInjectionSeverity.High;
            }
        }

        // Check for Base64 encoded instructions
        if (ContainsBase64Instructions(input))
        {
            _logger.LogWarning(
                "Base64 encoded prompt injection detected. InputSample: {Sample}",
                TruncateForLog(input));
            return PromptInjectionSeverity.High;
        }

        // Check medium severity patterns
        foreach (var pattern in MediumSeverityPatterns)
        {
            if (pattern.IsMatch(input))
            {
                _logger.LogInformation(
                    "Medium severity prompt injection pattern detected. Pattern: {Pattern}, InputSample: {Sample}",
                    pattern.ToString(),
                    TruncateForLog(input));
                return PromptInjectionSeverity.Medium;
            }
        }

        // Check low severity patterns
        foreach (var pattern in LowSeverityPatterns)
        {
            if (pattern.IsMatch(input))
            {
                _logger.LogDebug(
                    "Low severity prompt injection pattern detected. Pattern: {Pattern}",
                    pattern.ToString());
                return PromptInjectionSeverity.Low;
            }
        }

        return PromptInjectionSeverity.None;
    }

    private bool ContainsBase64Instructions(string input)
    {
        // Check for suspicious base64 patterns
        if (!Base64Pattern.IsMatch(input))
        {
            return false;
        }

        // Try to decode potential base64 strings
        var base64Regex = new Regex(@"[A-Za-z0-9+/=]{20,}", RegexOptions.Compiled);
        foreach (Match match in base64Regex.Matches(input))
        {
            try
            {
                var decoded = System.Text.Encoding.UTF8.GetString(
                    Convert.FromBase64String(match.Value));

                // Check if decoded content contains injection patterns
                foreach (var pattern in HighSeverityPatterns)
                {
                    if (pattern.IsMatch(decoded))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // Not valid base64, ignore
            }
        }

        return false;
    }

    private static string TruncateForLog(string input, int maxLength = 100)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        // Redact for security - only show first and last portions
        if (input.Length <= maxLength)
        {
            return $"[{input.Length} chars]";
        }

        return $"[{input.Length} chars]";
    }
}
