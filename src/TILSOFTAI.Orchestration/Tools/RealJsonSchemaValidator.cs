using System.Collections.Concurrent;
using System.Text.Json;
using Json.Schema;

namespace TILSOFTAI.Orchestration.Tools;

public sealed class RealJsonSchemaValidator : IJsonSchemaValidator
{
    private static readonly ConcurrentDictionary<string, JsonSchema> SchemaCache = new(StringComparer.Ordinal);

    public JsonSchemaValidationResult Validate(string schemaJson, string instanceJson)
    {
        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            return new JsonSchemaValidationResult(false, "Schema is empty.");
        }

        if (string.IsNullOrWhiteSpace(instanceJson))
        {
            return new JsonSchemaValidationResult(false, "Arguments JSON invalid: empty.");
        }

        JsonSchema schema;
        try
        {
            schema = SchemaCache.GetOrAdd(schemaJson, JsonSchema.FromText);
        }
        catch (Exception ex)
        {
            return new JsonSchemaValidationResult(false, $"Schema JSON invalid: {ex.Message}");
        }

        try
        {
            using var instance = JsonDocument.Parse(instanceJson);
            var result = schema.Evaluate(instance.RootElement);
            if (result.IsValid)
            {
                return new JsonSchemaValidationResult(true, null);
            }
        }
        catch (JsonException ex)
        {
            return new JsonSchemaValidationResult(false, $"Arguments JSON invalid: {ex.Message}");
        }

        return new JsonSchemaValidationResult(false, "Arguments JSON failed schema validation.");
    }
}
