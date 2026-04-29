using System.Text.Json;
using TILSOFTAI.Orchestration.Semantic;

namespace TILSOFTAI.Orchestration.AiRouting.Tools;

public sealed class CapabilityToolDescriptionBuilder
{
    public string Build(CapabilitySemanticMetadata capability)
    {
        var text = capability.Text;
        var lines = new List<string>
        {
            $"Business domain: {capability.Domain}",
            $"Purpose: {text?.Description ?? capability.CapabilityKey}"
        };

        if (!string.IsNullOrWhiteSpace(text?.UseWhen))
        {
            lines.Add($"Use when: {text.UseWhen}");
        }

        if (!string.IsNullOrWhiteSpace(text?.DoNotUseWhen))
        {
            lines.Add($"Do not use when: {text.DoNotUseWhen}");
        }

        var capabilityAliases = TopJsonValues(text?.Aliases, 6);
        if (capabilityAliases.Length > 0)
        {
            lines.Add($"Aliases: {string.Join(", ", capabilityAliases)}");
        }

        var examples = capability.Examples
            .OrderBy(example => example.SortOrder)
            .Select(example => example.Utterance)
            .Where(utterance => !string.IsNullOrWhiteSpace(utterance))
            .Take(3)
            .ToArray();
        if (examples.Length > 0)
        {
            lines.Add("Examples:");
            lines.AddRange(examples.Select(example => $"- {example}"));
        }

        var argumentLines = capability.Arguments
            .OrderBy(argument => argument.DisplayOrder)
            .Select(BuildArgumentLine)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        if (argumentLines.Length > 0)
        {
            lines.Add("Input requirements:");
            lines.AddRange(argumentLines);
        }

        if (!string.IsNullOrWhiteSpace(capability.ResultSchema))
        {
            lines.Add($"Result schema: {SummarizeJson(capability.ResultSchema)}");
        }

        if (!string.IsNullOrWhiteSpace(capability.AnswerPolicy))
        {
            lines.Add($"Answer policy: {SummarizeJson(capability.AnswerPolicy)}");
        }

        if (!string.IsNullOrWhiteSpace(capability.SensitivityPolicy))
        {
            lines.Add($"Sensitivity policy: {SummarizeJson(capability.SensitivityPolicy)}");
        }

        lines.Add($"Execution mode: {capability.ExecutionMode}");
        lines.Add("Safety: Never invent IDs. Ask clarification if required inputs are missing or ambiguous.");

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildArgumentLine(CapabilityArgumentMetadata argument)
    {
        var parameterName = CapabilityFunctionNameMapper.MapParameter(argument.ArgumentName);
        var text = argument.Text;
        var aliases = TopJsonValues(text?.Aliases, 4);
        var examples = TopJsonValues(text?.Examples, 3);

        var suffixes = new List<string>();
        if (aliases.Length > 0)
        {
            suffixes.Add($"Aliases: {string.Join(", ", aliases)}");
        }

        if (examples.Length > 0)
        {
            suffixes.Add($"Examples: {string.Join("; ", examples)}");
        }

        if (!string.IsNullOrWhiteSpace(text?.ClarificationQuestion))
        {
            suffixes.Add($"Clarify with: {text.ClarificationQuestion}");
        }

        var requirement = argument.IsRequired ? "Required" : "Optional";
        var description = text?.Description ?? argument.ArgumentName;
        var suffix = suffixes.Count == 0 ? string.Empty : $" {string.Join(". ", suffixes)}.";
        return $"- {parameterName}: {description} ({requirement}).{suffix}";
    }

    private static string[] TopJsonValues(string? json, int take)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<string>();
            }

            return document.RootElement
                .EnumerateArray()
                .Where(element => element.ValueKind == JsonValueKind.String)
                .Select(element => element.GetString())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Take(take)
                .ToArray()!;
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    private static string SummarizeJson(string json)
    {
        if (json.Length <= 160)
        {
            return json;
        }

        return json[..160] + "...";
    }
}
