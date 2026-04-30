using System.Text.Json;

namespace TILSOFTAI.Orchestration.Answering.Narration;

public sealed class AnswerNarrationResponseParser
{
    public bool TryParse(
        string? json,
        AnswerNarrationRequest request,
        int maxOutputCharacters,
        out AnswerNarrationResult result)
    {
        ArgumentNullException.ThrowIfNull(request);
        result = new AnswerNarrationResult { Text = string.Empty };

        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!document.RootElement.TryGetProperty("text", out var textElement)
                || textElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var text = textElement.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(text)
                || text.Length > maxOutputCharacters
                || ContainsBlockedReference(text, request))
            {
                return false;
            }

            result = new AnswerNarrationResult
            {
                Text = text,
                Confidence = ReadConfidence(document.RootElement),
                UsedColumns = ReadStringArray(document.RootElement, "usedColumns"),
                Warnings = ReadStringArray(document.RootElement, "warnings")
            };
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static double? ReadConfidence(JsonElement root)
    {
        if (!root.TryGetProperty("confidence", out var element)
            || element.ValueKind is not JsonValueKind.Number)
        {
            return null;
        }

        return element.TryGetDouble(out var value) ? value : null;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element)
            || element.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return element.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();
    }

    private static bool ContainsBlockedReference(string text, AnswerNarrationRequest request)
    {
        foreach (var hidden in request.SensitivityPolicy.HiddenColumns)
        {
            if (!string.IsNullOrWhiteSpace(hidden)
                && text.Contains(hidden, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
