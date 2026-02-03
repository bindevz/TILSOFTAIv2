using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Errors;
using TILSOFTAI.Domain.Validation;

namespace TILSOFTAI.Infrastructure.Validation;

/// <summary>
/// Centralized input validation and sanitization service.
/// </summary>
public sealed class InputValidator : IInputValidator
{
    private readonly ValidationOptions _options;
    private readonly PromptInjectionDetector _injectionDetector;
    private readonly ILogger<InputValidator> _logger;
    private readonly Regex[] _denyPatterns;
    private readonly HashSet<UnicodeCategory> _allowedCategories;

    // Precompiled HTML tag pattern for sanitization
    private static readonly Regex HtmlTagPattern = new(@"<[^>]+>", RegexOptions.Compiled);

    public InputValidator(
        IOptions<ValidationOptions> options,
        PromptInjectionDetector injectionDetector,
        ILogger<InputValidator> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _injectionDetector = injectionDetector ?? throw new ArgumentNullException(nameof(injectionDetector));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Precompile deny patterns
        _denyPatterns = _options.DenyPatterns
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => new Regex(p, RegexOptions.Compiled | RegexOptions.IgnoreCase))
            .ToArray();

        // Parse allowed Unicode categories
        _allowedCategories = ParseUnicodeCategories(_options.AllowedUnicodeCategories);
    }

    public ValidationResult ValidateUserInput(string? input, InputContext context)
    {
        if (input is null)
        {
            return ValidationResult.Success(string.Empty, string.Empty);
        }

        var originalInput = input;

        // Step 1: Check length limits
        var maxLength = context.MaxLength ?? GetMaxLengthForContext(context.Type);
        if (input.Length > maxLength)
        {
            _logger.LogWarning(
                "Input validation failed: Input too long. Length: {ActualLength}, MaxLength: {MaxLength}, Field: {Field}",
                input.Length, maxLength, context.FieldName);

            return ValidationResult.Failure(originalInput, new ValidationError
            {
                Code = ErrorCode.InputTooLong,
                Field = context.FieldName,
                Message = $"Input exceeds maximum length of {maxLength} characters.",
                MessageKey = "validation.input_too_long",
                Data = new Dictionary<string, object>
                {
                    ["maxLength"] = maxLength,
                    ["actualLength"] = input.Length
                }
            });
        }

        // Step 2: Unicode normalization (NFC)
        if (_options.NormalizeUnicode && context.RequireUnicodeNormalization)
        {
            input = input.Normalize(NormalizationForm.FormC);
        }

        // Step 3: Remove/escape dangerous patterns (null bytes, control chars)
        if (_options.RemoveControlCharacters)
        {
            input = RemoveControlCharacters(input);
        }

        // Step 4: HTML tag sanitization (if enabled)
        if (_options.SanitizeHtmlTags)
        {
            input = SanitizeHtmlTags(input);
        }

        // Step 5: Prompt injection detection (if enabled for this context)
        PromptInjectionSeverity injectionSeverity = PromptInjectionSeverity.None;
        if (_options.EnablePromptInjectionDetection && context.EnablePromptInjectionDetection)
        {
            injectionSeverity = _injectionDetector.Detect(input);

            if (injectionSeverity != PromptInjectionSeverity.None)
            {
                var shouldBlock = _options.BlockOnPromptInjection &&
                                  SeverityMeetsThreshold(injectionSeverity, _options.BlockSeverityThreshold);

                if (shouldBlock)
                {
                    _logger.LogWarning(
                        "Prompt injection blocked. Severity: {Severity}, Field: {Field}",
                        injectionSeverity, context.FieldName);

                    return ValidationResult.Failure(originalInput, new ValidationError
                    {
                        Code = ErrorCode.PromptInjectionDetected,
                        Field = context.FieldName,
                        Message = "Potential prompt injection detected.",
                        MessageKey = "validation.prompt_injection_detected",
                        Data = new Dictionary<string, object>
                        {
                            ["severity"] = injectionSeverity.ToString()
                        }
                    });
                }
            }
        }

        // Step 6: Validate against deny patterns
        foreach (var pattern in _denyPatterns)
        {
            if (pattern.IsMatch(input))
            {
                _logger.LogWarning(
                    "Input validation failed: Forbidden pattern matched. Pattern: {Pattern}, Field: {Field}",
                    pattern.ToString(), context.FieldName);

                return ValidationResult.Failure(originalInput, new ValidationError
                {
                    Code = ErrorCode.ForbiddenPattern,
                    Field = context.FieldName,
                    Message = "Input contains forbidden pattern.",
                    MessageKey = "validation.forbidden_pattern"
                });
            }
        }

        // Log if sanitization changed the input
        if (input != originalInput)
        {
            _logger.LogDebug(
                "Input sanitized. OriginalLength: {OriginalLength}, SanitizedLength: {SanitizedLength}, Field: {Field}",
                originalInput.Length, input.Length, context.FieldName);
        }

        // Return success with any injection warnings
        if (injectionSeverity != PromptInjectionSeverity.None)
        {
            return ValidationResult.SuccessWithWarning(originalInput, input, injectionSeverity);
        }

        return ValidationResult.Success(originalInput, input);
    }

    public ValidationResult ValidateToolArguments(string? argumentsJson, string toolName)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return ValidationResult.Success(argumentsJson ?? string.Empty, argumentsJson ?? string.Empty);
        }

        var context = InputContext.ForToolArgument(toolName, _options.MaxToolArgumentLength);

        // Check length
        if (argumentsJson.Length > _options.MaxToolArgumentLength)
        {
            _logger.LogWarning(
                "Tool arguments validation failed: Arguments too long. Length: {ActualLength}, MaxLength: {MaxLength}, Tool: {Tool}",
                argumentsJson.Length, _options.MaxToolArgumentLength, toolName);

            return ValidationResult.Failure(argumentsJson, new ValidationError
            {
                Code = ErrorCode.InputTooLong,
                Field = "arguments",
                Message = $"Tool arguments exceed maximum length of {_options.MaxToolArgumentLength} characters.",
                MessageKey = "validation.tool_args_too_long",
                Data = new Dictionary<string, object>
                {
                    ["maxLength"] = _options.MaxToolArgumentLength,
                    ["actualLength"] = argumentsJson.Length,
                    ["toolName"] = toolName
                }
            });
        }

        // Validate JSON structure
        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            // Recursively sanitize string values within the JSON
            var sanitizedJson = SanitizeJsonValues(doc.RootElement);
            return ValidationResult.Success(argumentsJson, sanitizedJson);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "Tool arguments validation failed: Invalid JSON. Tool: {Tool}",
                toolName);

            return ValidationResult.Failure(argumentsJson, new ValidationError
            {
                Code = ErrorCode.InvalidInput,
                Field = "arguments",
                Message = "Tool arguments must be valid JSON.",
                MessageKey = "validation.invalid_json"
            });
        }
    }

    public ValidationResult ValidateToolArguments(JsonElement arguments, string toolName)
    {
        var json = arguments.GetRawText();
        return ValidateToolArguments(json, toolName);
    }

    private int GetMaxLengthForContext(InputContextType type)
    {
        return type switch
        {
            InputContextType.ChatMessage => _options.MaxInputLength,
            InputContextType.ToolArgument => _options.MaxToolArgumentLength,
            InputContextType.Header => 8192,
            InputContextType.QueryParam => 2048,
            _ => _options.MaxInputLength
        };
    }

    private string RemoveControlCharacters(string input)
    {
        var allowedChars = new HashSet<char>(_options.AllowedControlCharacters);
        var sb = new StringBuilder(input.Length);

        foreach (var c in input)
        {
            // Allow whitelisted control characters
            if (allowedChars.Contains(c))
            {
                sb.Append(c);
                continue;
            }

            // Remove null bytes unconditionally
            if (c == '\0')
            {
                continue;
            }

            // Check Unicode category
            var category = char.GetUnicodeCategory(c);
            if (category == UnicodeCategory.Control ||
                category == UnicodeCategory.Format ||
                category == UnicodeCategory.PrivateUse ||
                category == UnicodeCategory.Surrogate)
            {
                continue;
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    private static string SanitizeHtmlTags(string input)
    {
        // Replace HTML tags with empty string
        // This is a simple sanitization - more complex scenarios might need a proper HTML sanitizer
        return HtmlTagPattern.Replace(input, string.Empty);
    }

    private string SanitizeJsonValues(JsonElement element)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        SanitizeJsonElement(element, writer);

        writer.Flush();
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private void SanitizeJsonElement(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    SanitizeJsonElement(property.Value, writer);
                }
                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    SanitizeJsonElement(item, writer);
                }
                writer.WriteEndArray();
                break;

            case JsonValueKind.String:
                var stringValue = element.GetString() ?? string.Empty;
                // Apply basic sanitization to string values
                if (_options.RemoveControlCharacters)
                {
                    stringValue = RemoveControlCharacters(stringValue);
                }
                writer.WriteStringValue(stringValue);
                break;

            case JsonValueKind.Number:
                if (element.TryGetInt64(out var longValue))
                {
                    writer.WriteNumberValue(longValue);
                }
                else if (element.TryGetDouble(out var doubleValue))
                {
                    writer.WriteNumberValue(doubleValue);
                }
                else
                {
                    writer.WriteRawValue(element.GetRawText());
                }
                break;

            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;

            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;

            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;

            default:
                writer.WriteRawValue(element.GetRawText());
                break;
        }
    }

    private static HashSet<UnicodeCategory> ParseUnicodeCategories(string[] categoryNames)
    {
        var categories = new HashSet<UnicodeCategory>();

        foreach (var name in categoryNames)
        {
            switch (name.ToLowerInvariant())
            {
                case "letter":
                    categories.Add(UnicodeCategory.UppercaseLetter);
                    categories.Add(UnicodeCategory.LowercaseLetter);
                    categories.Add(UnicodeCategory.TitlecaseLetter);
                    categories.Add(UnicodeCategory.ModifierLetter);
                    categories.Add(UnicodeCategory.OtherLetter);
                    break;
                case "number":
                    categories.Add(UnicodeCategory.DecimalDigitNumber);
                    categories.Add(UnicodeCategory.LetterNumber);
                    categories.Add(UnicodeCategory.OtherNumber);
                    break;
                case "punctuation":
                    categories.Add(UnicodeCategory.ConnectorPunctuation);
                    categories.Add(UnicodeCategory.DashPunctuation);
                    categories.Add(UnicodeCategory.OpenPunctuation);
                    categories.Add(UnicodeCategory.ClosePunctuation);
                    categories.Add(UnicodeCategory.InitialQuotePunctuation);
                    categories.Add(UnicodeCategory.FinalQuotePunctuation);
                    categories.Add(UnicodeCategory.OtherPunctuation);
                    break;
                case "symbol":
                    categories.Add(UnicodeCategory.MathSymbol);
                    categories.Add(UnicodeCategory.CurrencySymbol);
                    categories.Add(UnicodeCategory.ModifierSymbol);
                    categories.Add(UnicodeCategory.OtherSymbol);
                    break;
                case "separator":
                    categories.Add(UnicodeCategory.SpaceSeparator);
                    categories.Add(UnicodeCategory.LineSeparator);
                    categories.Add(UnicodeCategory.ParagraphSeparator);
                    break;
                case "mark":
                    categories.Add(UnicodeCategory.NonSpacingMark);
                    categories.Add(UnicodeCategory.SpacingCombiningMark);
                    categories.Add(UnicodeCategory.EnclosingMark);
                    break;
            }
        }

        return categories;
    }

    private static bool SeverityMeetsThreshold(PromptInjectionSeverity severity, string threshold)
    {
        var thresholdLevel = threshold.ToLowerInvariant() switch
        {
            "low" => PromptInjectionSeverity.Low,
            "medium" => PromptInjectionSeverity.Medium,
            "high" => PromptInjectionSeverity.High,
            _ => PromptInjectionSeverity.High
        };

        return severity >= thresholdLevel;
    }
}
