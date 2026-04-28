using System.Text.Json;
using System.Text.Json.Nodes;
using TILSOFTAI.Orchestration.Semantic;

namespace TILSOFTAI.Orchestration.AiRouting.Tools;

public sealed class CapabilityParameterSchemaBuilder
{
    public JsonObject Build(
        IReadOnlyList<CapabilityArgumentMetadata> arguments,
        out IReadOnlyDictionary<string, string> modelToCapabilityArgumentMap)
    {
        var properties = new JsonObject();
        var required = new JsonArray();
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var argument in arguments.OrderBy(argument => argument.DisplayOrder))
        {
            var modelName = CapabilityFunctionNameMapper.MapParameter(argument.ArgumentName);
            map[modelName] = argument.ArgumentName;

            var schema = BuildPropertySchema(argument);
            properties[modelName] = schema;

            if (argument.IsRequired)
            {
                required.Add(modelName);
            }
        }

        modelToCapabilityArgumentMap = map;

        return new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["properties"] = properties,
            ["required"] = required
        };
    }

    private static JsonObject BuildPropertySchema(CapabilityArgumentMetadata argument)
    {
        var schema = new JsonObject
        {
            ["type"] = MapJsonType(argument.DataType),
            ["description"] = argument.Text?.Description ?? argument.ArgumentName
        };

        if (argument.DataType.Equals("date", StringComparison.OrdinalIgnoreCase))
        {
            schema["format"] = "date";
        }
        else if (argument.DataType.Equals("datetime", StringComparison.OrdinalIgnoreCase))
        {
            schema["format"] = "date-time";
        }

        ApplyValidationRule(schema, argument.ValidationRule);
        return schema;
    }

    private static string MapJsonType(string dataType)
    {
        if (dataType.Equals("integer", StringComparison.OrdinalIgnoreCase)
            || dataType.Equals("int", StringComparison.OrdinalIgnoreCase))
        {
            return "integer";
        }

        if (dataType.Equals("decimal", StringComparison.OrdinalIgnoreCase)
            || dataType.Equals("number", StringComparison.OrdinalIgnoreCase)
            || dataType.Equals("float", StringComparison.OrdinalIgnoreCase))
        {
            return "number";
        }

        if (dataType.Equals("boolean", StringComparison.OrdinalIgnoreCase)
            || dataType.Equals("bool", StringComparison.OrdinalIgnoreCase))
        {
            return "boolean";
        }

        if (dataType.Equals("object", StringComparison.OrdinalIgnoreCase)) return "object";
        if (dataType.Equals("array", StringComparison.OrdinalIgnoreCase)) return "array";
        return "string";
    }

    private static void ApplyValidationRule(JsonObject schema, string? validationRule)
    {
        if (string.IsNullOrWhiteSpace(validationRule))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(validationRule);
            var root = document.RootElement;

            if (root.TryGetProperty("allowed", out var allowed) && allowed.ValueKind == JsonValueKind.Array)
            {
                var enumValues = new JsonArray();
                foreach (var value in allowed.EnumerateArray())
                {
                    if (value.ValueKind == JsonValueKind.String)
                    {
                        enumValues.Add(value.GetString());
                    }
                }

                schema["enum"] = enumValues;
            }

            if (root.TryGetProperty("pattern", out var pattern) && pattern.ValueKind == JsonValueKind.String)
            {
                schema["pattern"] = pattern.GetString();
            }

            if (root.TryGetProperty("format", out var format) && format.ValueKind == JsonValueKind.String)
            {
                schema["format"] = format.GetString();
            }

            if (root.TryGetProperty("minLength", out var minLength) && minLength.TryGetInt32(out var minLengthValue))
            {
                schema["minLength"] = minLengthValue;
            }

            if (root.TryGetProperty("minimum", out var minimum) && minimum.TryGetDecimal(out var minimumValue))
            {
                schema["minimum"] = minimumValue;
            }

            if (root.TryGetProperty("maximum", out var maximum) && maximum.TryGetDecimal(out var maximumValue))
            {
                schema["maximum"] = maximumValue;
            }
        }
        catch (JsonException)
        {
            schema["x-validationRuleParseError"] = true;
        }
    }
}
