using System.Collections.Concurrent;
using System.Text.Json;
using Json.Schema;

namespace TILSOFTAI.Orchestration.Tools;

public sealed class RealJsonSchemaValidator : IJsonSchemaValidator
{
    private static readonly ConcurrentDictionary<string, JsonSchema> SchemaCache = new(StringComparer.Ordinal);
    private static readonly EvaluationOptions EvaluationOptions = new()
    {
        OutputFormat = OutputFormat.List,
        RequireFormatValidation = true
    };

    public JsonSchemaValidationResult Validate(string schemaJson, string instanceJson)
    {
        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            return new JsonSchemaValidationResult(false, Array.Empty<string>(), "Schema is empty.");
        }

        if (string.IsNullOrWhiteSpace(instanceJson))
        {
            return new JsonSchemaValidationResult(false, Array.Empty<string>(), "Arguments JSON invalid: empty.");
        }

        JsonSchema schema;
        try
        {
            schema = SchemaCache.GetOrAdd(schemaJson, JsonSchema.FromText);
        }
        catch (Exception ex)
        {
            return new JsonSchemaValidationResult(false, Array.Empty<string>(), $"Schema JSON invalid: {ex.Message}");
        }

        try
        {
            using var instance = JsonDocument.Parse(instanceJson);
            var result = schema.Evaluate(instance.RootElement, EvaluationOptions);
            if (result.IsValid)
            {
                return new JsonSchemaValidationResult(true, Array.Empty<string>(), null);
            }

            var errors = new List<string>();
            CollectErrors(result, errors);
            return new JsonSchemaValidationResult(
                false,
                errors,
                "Arguments JSON failed schema validation.");
        }
        catch (JsonException ex)
        {
            return new JsonSchemaValidationResult(false, Array.Empty<string>(), $"Arguments JSON invalid: {ex.Message}");
        }
    }

    private static void CollectErrors(EvaluationResults results, List<string> errors)
    {
        if (!results.IsValid && results.Errors is { Count: > 0 })
        {
            var path = NormalizePath(results.InstanceLocation?.ToString());
            foreach (var kvp in results.Errors)
            {
                var message = kvp.Value?.ToString() ?? kvp.Key;
                errors.Add($"{path}: {message}");
            }
        }

        if (results.Details is null)
        {
            return;
        }

        foreach (var detail in results.Details)
        {
            CollectErrors(detail, errors);
        }
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        return path.StartsWith('/') ? path : "/" + path;
    }
}
