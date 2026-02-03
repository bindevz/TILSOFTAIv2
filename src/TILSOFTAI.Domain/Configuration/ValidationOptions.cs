namespace TILSOFTAI.Domain.Configuration;

/// <summary>
/// Configuration options for input validation and sanitization.
/// </summary>
public sealed class ValidationOptions
{
    /// <summary>
    /// Maximum input length for chat messages. Default: 32000 characters.
    /// </summary>
    public int MaxInputLength { get; set; } = 32000;

    /// <summary>
    /// Maximum length for tool arguments JSON. Default: 8000 characters.
    /// </summary>
    public int MaxToolArgumentLength { get; set; } = 8000;

    /// <summary>
    /// Allowed Unicode categories for input. Default includes common text categories.
    /// Valid values: Letter, Number, Punctuation, Symbol, Separator, Mark
    /// </summary>
    public string[] AllowedUnicodeCategories { get; set; } = new[]
    {
        "Letter",
        "Number",
        "Punctuation",
        "Symbol",
        "Separator",
        "Mark"
    };

    /// <summary>
    /// Regex patterns to reject. Input matching any pattern will be rejected.
    /// </summary>
    public string[] DenyPatterns { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Whether to sanitize HTML tags from input. Default: true.
    /// </summary>
    public bool SanitizeHtmlTags { get; set; } = true;

    /// <summary>
    /// Whether to normalize Unicode to NFC form. Default: true.
    /// </summary>
    public bool NormalizeUnicode { get; set; } = true;

    /// <summary>
    /// Whether to enable prompt injection detection. Default: true.
    /// </summary>
    public bool EnablePromptInjectionDetection { get; set; } = true;

    /// <summary>
    /// Whether to block requests when high-severity prompt injection is detected.
    /// When false, only logs a warning. Default: false.
    /// </summary>
    public bool BlockOnPromptInjection { get; set; } = false;

    /// <summary>
    /// Minimum severity level to block when BlockOnPromptInjection is true.
    /// Values: Low, Medium, High. Default: High.
    /// </summary>
    public string BlockSeverityThreshold { get; set; } = "High";

    /// <summary>
    /// Whether to remove null bytes and control characters. Default: true.
    /// </summary>
    public bool RemoveControlCharacters { get; set; } = true;

    /// <summary>
    /// Characters to preserve even when removing control characters.
    /// Default: tab, newline, carriage return.
    /// </summary>
    public char[] AllowedControlCharacters { get; set; } = new[] { '\t', '\n', '\r' };
}
