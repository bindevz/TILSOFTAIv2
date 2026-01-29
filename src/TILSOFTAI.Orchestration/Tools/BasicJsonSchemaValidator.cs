using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace TILSOFTAI.Orchestration.Tools;

public sealed class BasicJsonSchemaValidator : IJsonSchemaValidator
{
    private static readonly ConcurrentDictionary<string, JsonSchema> _schemaCache = new();
    private static readonly EvaluationOptions _evaluationOptions = new()
    {
        OutputFormat = OutputFormat.List,
        RequireFormatValidation = true
    };

    public JsonSchemaValidationResult Validate(string schemaJson, string instanceJson)
    {
        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            return new JsonSchemaValidationResult(false, "Schema is empty.");
        }

        if (string.IsNullOrWhiteSpace(instanceJson))
        {
            return new JsonSchemaValidationResult(false, "Arguments JSON is empty.");
        }

        // Parse schema (with caching)
        JsonSchema schema;
        try
        {
            var schemaHash = ComputeHash(schemaJson);
            schema = _schemaCache.GetOrAdd(schemaHash, _ => JsonSchema.FromText(schemaJson));
        }
        catch (Exception ex)
        {
            return new JsonSchemaValidationResult(false, $"Schema JSON invalid: {ex.Message}");
        }

        // Parse instance
        JsonNode? instance;
        try
        {
            instance = JsonNode.Parse(instanceJson);
        }
        catch (JsonException ex)
        {
            return new JsonSchemaValidationResult(false, $"Arguments JSON invalid: {ex.Message}");
        }

        if (instance is null)
        {
            return new JsonSchemaValidationResult(false, "Arguments JSON is null.");
        }

        // Evaluate schema
        var result = schema.Evaluate(instance, _evaluationOptions);
        if (result.IsValid)
        {
            return new JsonSchemaValidationResult(true, null);
        }

        // Collect errors
        var errors = new List<string>();
        CollectErrors(result, errors);

        var errorMessage = errors.Count > 0
            ? string.Join("; ", errors)
            : "Schema validation failed.";

        return new JsonSchemaValidationResult(false, errorMessage);
    }

    private static void CollectErrors(EvaluationResults results, List<string> errors)
    {
        // Check if this result has errors
        if (!results.IsValid && results.Errors != null && results.Errors.Count > 0)
        {
            var path = results.InstanceLocation?.ToString() ?? "/";
            foreach (var kvp in results.Errors)
            {
                errors.Add($"{path}: {kvp.Key} - {kvp.Value}");
            }
        }

        // Recursively collect errors from nested details
        if (results.Details != null)
        {
            foreach (var detail in results.Details)
            {
                CollectErrors(detail, errors);
            }
        }
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}
