using System.Text.Json;
using System.Text.RegularExpressions;
using TILSOFTAI.Orchestration.Observability;

namespace TILSOFTAI.Infrastructure.Observability;

public sealed class BasicLogRedactor : ILogRedactor
{
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

    private static readonly string[] SensitiveKeys = 
    {
        "password", "token", "email", "phone", "address", 
        "ssn", "credit_card", "api_key", "secret", "apikey"
    };

    public (string redacted, bool changed) RedactText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return (text, false);
        }

        var redacted = text;
        var changed = false;

        // Redact emails
        if (EmailRegex.IsMatch(redacted))
        {
            redacted = EmailRegex.Replace(redacted, "***@***.***");
            changed = true;
        }

        // Redact phone numbers
        if (PhoneRegex.IsMatch(redacted))
        {
            redacted = PhoneRegex.Replace(redacted, "***-***-****");
            changed = true;
        }

        // Redact credit card patterns
        if (CreditCardRegex.IsMatch(redacted))
        {
            redacted = CreditCardRegex.Replace(redacted, "****-****-****-****");
            changed = true;
        }

        // Redact long hex IDs (keep shorter ones for legitimate use cases)
        if (LongHexIdRegex.IsMatch(redacted))
        {
            redacted = LongHexIdRegex.Replace(redacted, "[REDACTED_ID]");
            changed = true;
        }

        return (redacted, changed);
    }

    public (string redacted, bool changed) RedactJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return (json, false);
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var redacted = RedactJsonElement(doc.RootElement, out var changed);
            return (redacted, changed);
        }
        catch (JsonException)
        {
            // If it's not valid JSON, treat as text
            return RedactText(json);
        }
    }

    private string RedactJsonElement(JsonElement element, out bool changed)
    {
        changed = false;

        if (element.ValueKind == JsonValueKind.Object)
        {
            var obj = new Dictionary<string, object?>();
            var anyChanged = false;

            foreach (var property in element.EnumerateObject())
            {
                var key = property.Name;
                var value = property.Value;

                // Check if key is sensitive
                var isSensitiveKey = SensitiveKeys.Any(sk => 
                    key.Contains(sk, StringComparison.OrdinalIgnoreCase));

                if (isSensitiveKey && value.ValueKind == JsonValueKind.String)
                {
                    obj[key] = "[REDACTED]";
                    anyChanged = true;
                }
                else if (value.ValueKind == JsonValueKind.Object || value.ValueKind == JsonValueKind.Array)
                {
                    var nested = RedactJsonElement(value, out var nestedChanged);
                    obj[key] = JsonDocument.Parse(nested).RootElement;
                    if (nestedChanged) anyChanged = true;
                }
                else if (value.ValueKind == JsonValueKind.String)
                {
                    var stringValue = value.GetString() ?? string.Empty;
                    var (redactedText, textChanged) = RedactText(stringValue);
                    obj[key] = redactedText;
                    if (textChanged) anyChanged = true;
                }
                else
                {
                    obj[key] = GetJsonValue(value);
                }
            }

            changed = anyChanged;
            return JsonSerializer.Serialize(obj);
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var array = new List<object?>();
            var anyChanged = false;

            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object || item.ValueKind == JsonValueKind.Array)
                {
                    var nested = RedactJsonElement(item, out var nestedChanged);
                    array.Add(JsonDocument.Parse(nested).RootElement);
                    if (nestedChanged) anyChanged = true;
                }
                else if (item.ValueKind == JsonValueKind.String)
                {
                    var stringValue = item.GetString() ?? string.Empty;
                    var (redactedText, textChanged) = RedactText(stringValue);
                    array.Add(redactedText);
                    if (textChanged) anyChanged = true;
                }
                else
                {
                    array.Add(GetJsonValue(item));
                }
            }

            changed = anyChanged;
            return JsonSerializer.Serialize(array);
        }
        else
        {
            return JsonSerializer.Serialize(GetJsonValue(element));
        }
    }

    private static object? GetJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number =>element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }
}
