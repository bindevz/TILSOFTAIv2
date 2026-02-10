using System.Text.Json;
using System.Text.RegularExpressions;

namespace TILSOFTAI.Orchestration.Policies;

/// <summary>
/// Stateless evaluator for ReAct follow-up rules.
/// Evaluates rules against tool result JSON and returns matched rules.
/// Supports operators: exists, ==, !=, &gt;, &gt;=, &lt;, &lt;=, contains.
/// </summary>
public sealed class ReActFollowUpEvaluator
{
    /// <summary>
    /// Evaluate which rules are triggered by the given tool result.
    /// </summary>
    public IReadOnlyList<ReActFollowUpRule> Evaluate(
        IReadOnlyList<ReActFollowUpRule> rules,
        string toolName,
        string toolResultJson)
    {
        if (rules.Count == 0 || string.IsNullOrWhiteSpace(toolResultJson))
            return Array.Empty<ReActFollowUpRule>();

        JsonElement root;
        try
        {
            using var doc = JsonDocument.Parse(toolResultJson);
            root = doc.RootElement.Clone();
        }
        catch
        {
            return Array.Empty<ReActFollowUpRule>();
        }

        var matched = new List<ReActFollowUpRule>();

        foreach (var rule in rules)
        {
            // Only evaluate rules that match the trigger tool (or have no tool filter)
            if (!string.IsNullOrEmpty(rule.ToolName)
                && !string.Equals(rule.ToolName, toolName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (EvaluateCondition(root, rule.JsonPath, rule.Operator, rule.CompareValue))
            {
                matched.Add(rule);
            }
        }

        return matched;
    }

    /// <summary>
    /// Resolve argument template placeholders from tool result JSON.
    /// Replaces {{$.PropertyName}} with actual values from the JSON.
    /// </summary>
    public string? ResolveArgsTemplate(string? argsTemplate, string toolResultJson)
    {
        if (string.IsNullOrWhiteSpace(argsTemplate))
            return null;

        JsonElement root;
        try
        {
            using var doc = JsonDocument.Parse(toolResultJson);
            root = doc.RootElement.Clone();
        }
        catch
        {
            return null;
        }

        return Regex.Replace(argsTemplate, @"\{\{\$\.(\w+)\}\}", match =>
        {
            var propName = match.Groups[1].Value;
            var value = ExtractValue(root, "$." + propName);
            return value ?? match.Value; // Keep original placeholder if not found
        });
    }

    private static bool EvaluateCondition(JsonElement root, string jsonPath, string op, string? compareValue)
    {
        var value = ExtractValue(root, jsonPath);

        if (string.Equals(op, "exists", StringComparison.OrdinalIgnoreCase))
        {
            return value is not null;
        }

        if (value is null)
            return false;

        return op switch
        {
            "==" => string.Equals(value, compareValue, StringComparison.OrdinalIgnoreCase),
            "!=" => !string.Equals(value, compareValue, StringComparison.OrdinalIgnoreCase),
            ">" => TryCompareNumeric(value, compareValue, (a, b) => a > b),
            ">=" => TryCompareNumeric(value, compareValue, (a, b) => a >= b),
            "<" => TryCompareNumeric(value, compareValue, (a, b) => a < b),
            "<=" => TryCompareNumeric(value, compareValue, (a, b) => a <= b),
            "contains" => compareValue is not null && value.Contains(compareValue, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static string? ExtractValue(JsonElement root, string jsonPath)
    {
        // Simple $.PropertyName extraction (single level)
        if (!jsonPath.StartsWith("$."))
            return null;

        var propName = jsonPath[2..];
        var target = root;

        // Handle nested JSON: if root is a wrapper array, try first element
        if (target.ValueKind == JsonValueKind.Array && target.GetArrayLength() > 0)
        {
            target = target[0];
        }

        if (target.ValueKind != JsonValueKind.Object)
            return null;

        if (!target.TryGetProperty(propName, out var prop))
        {
            // Case-insensitive fallback
            foreach (var p in target.EnumerateObject())
            {
                if (string.Equals(p.Name, propName, StringComparison.OrdinalIgnoreCase))
                {
                    prop = p.Value;
                    goto found;
                }
            }
            return null;
        }

        found:
        return prop.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            JsonValueKind.String => prop.GetString(),
            JsonValueKind.Number => prop.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => prop.GetRawText()
        };
    }

    private static bool TryCompareNumeric(string? value, string? compareValue, Func<decimal, decimal, bool> comparison)
    {
        if (decimal.TryParse(value, out var a) && decimal.TryParse(compareValue, out var b))
        {
            return comparison(a, b);
        }
        return false;
    }
}
