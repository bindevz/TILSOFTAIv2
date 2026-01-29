using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Orchestration.Compaction;

public sealed class ToolResultCompactor
{
    public string CompactJson(string rawJson, int maxBytes, CompactionRules rules)
    {
        if (maxBytes <= 0)
        {
            maxBytes = 16000;
        }

        rawJson ??= string.Empty;
        var originalSize = Encoding.UTF8.GetByteCount(rawJson);
        var removedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var truncated = false;

        JsonNode? dataNode;
        try
        {
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                dataNode = new JsonObject();
            }
            else
            {
                using var doc = JsonDocument.Parse(rawJson);
                dataNode = CompactElement(doc.RootElement, rules, removedFields, ref truncated);
            }
        }
        catch (JsonException ex)
        {
            return SerializeFallback(rawJson, originalSize, maxBytes, removedFields, true, $"Invalid JSON: {ex.Message}");
        }

        var payload = AttachMetadata(dataNode, originalSize, removedFields, truncated);
        var serialized = payload.ToJsonString(new JsonSerializerOptions { WriteIndented = false });

        if (Encoding.UTF8.GetByteCount(serialized) > maxBytes)
        {
            return SerializeFallback(rawJson, originalSize, maxBytes, removedFields, true, "Compacted output exceeded max bytes.");
        }

        return serialized;
    }

    private static JsonNode CompactElement(JsonElement element, CompactionRules rules, HashSet<string> removedFields, ref bool truncated)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var obj = new JsonObject();
                foreach (var property in element.EnumerateObject())
                {
                    if (ShouldRemove(property.Name, rules.RemoveFields))
                    {
                        removedFields.Add(property.Name);
                        continue;
                    }

                    obj[property.Name] = CompactElement(property.Value, rules, removedFields, ref truncated);
                }
                return obj;
            }
            case JsonValueKind.Array:
            {
                var items = new List<JsonNode>();
                foreach (var item in element.EnumerateArray())
                {
                    items.Add(CompactElement(item, rules, removedFields, ref truncated));
                }

                if (rules.MaxArrayLength > 0 && items.Count > rules.MaxArrayLength)
                {
                    truncated = true;
                    var headCount = Math.Clamp(rules.HeadCount, 0, items.Count);
                    var tailCount = Math.Clamp(rules.TailCount, 0, Math.Max(0, items.Count - headCount));
                var headItems = new JsonArray(items.Take(headCount).ToArray());
                var tailItems = new JsonArray(items.Skip(items.Count - tailCount).ToArray());

                    return new JsonObject
                    {
                        ["_truncated"] = true,
                        ["totalCount"] = items.Count,
                        ["head"] = headItems,
                        ["tail"] = tailItems
                    };
                }

                return new JsonArray(items.ToArray());
            }
            default:
                return JsonNode.Parse(element.GetRawText()) ?? new JsonObject();
        }
    }

    private static bool ShouldRemove(string name, string[] removeFields)
    {
        if (removeFields.Length == 0)
        {
            return false;
        }

        return removeFields.Any(field => string.Equals(field, name, StringComparison.OrdinalIgnoreCase));
    }

    private static JsonNode AttachMetadata(JsonNode? dataNode, int originalSize, HashSet<string> removedFields, bool truncated)
    {
        var meta = new JsonObject
        {
            ["truncated"] = truncated,
            ["removedFields"] = new JsonArray(removedFields.Select(field => JsonValue.Create(field)).ToArray()),
            ["originalSizeBytes"] = originalSize
        };

        if (dataNode is JsonObject obj)
        {
            obj["_compaction"] = meta;
            return obj;
        }

        return new JsonObject
        {
            ["data"] = dataNode,
            ["_compaction"] = meta
        };
    }

    private static string SerializeFallback(string rawJson, int originalSize, int maxBytes, HashSet<string> removedFields, bool truncated, string error)
    {
        var fallback = new JsonObject
        {
            ["data"] = string.Empty,
            ["_compaction"] = new JsonObject
            {
                ["truncated"] = truncated,
                ["removedFields"] = new JsonArray(removedFields.Select(field => JsonValue.Create(field)).ToArray()),
                ["originalSizeBytes"] = originalSize,
                ["maxBytes"] = maxBytes,
                ["error"] = error
            }
        };

        if (!string.IsNullOrWhiteSpace(rawJson))
        {
            fallback["data"] = rawJson.Length > 512 ? rawJson[..512] : rawJson;
        }

        return fallback.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }
}
