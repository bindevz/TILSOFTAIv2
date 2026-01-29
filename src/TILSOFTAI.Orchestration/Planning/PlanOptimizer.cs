using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Atomic;
using TILSOFTAI.Orchestration.Tools;

namespace TILSOFTAI.Orchestration.Planning;

public sealed class PlanOptimizer
{
    private static readonly string[] AllowedFields =
    {
        "datasetKey",
        "select",
        "where",
        "groupBy",
        "orderBy",
        "limit",
        "offset",
        "timeRange",
        "drilldown"
    };

    private readonly IJsonSchemaValidator _schemaValidator;
    private readonly IAtomicCatalogProvider _catalogProvider;
    private readonly AtomicOptions _atomicOptions;

    public PlanOptimizer(
        IJsonSchemaValidator schemaValidator,
        IAtomicCatalogProvider catalogProvider,
        IOptions<AtomicOptions> atomicOptions)
    {
        _schemaValidator = schemaValidator ?? throw new ArgumentNullException(nameof(schemaValidator));
        _catalogProvider = catalogProvider ?? throw new ArgumentNullException(nameof(catalogProvider));
        _atomicOptions = atomicOptions?.Value ?? throw new ArgumentNullException(nameof(atomicOptions));
    }

    public async Task<PlanValidationResult> ValidateAsync(string planJson, TilsoftExecutionContext context, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(planJson))
        {
            return PlanValidationResult.Fail("Plan JSON is empty.");
        }

        var schemaValidation = _schemaValidator.Validate(AtomicPlanSchema, planJson);
        if (!schemaValidation.IsValid)
        {
            return PlanValidationResult.Fail(schemaValidation.Error ?? "Plan JSON failed schema validation.");
        }

        JsonNode? planNode;
        try
        {
            planNode = JsonNode.Parse(planJson);
        }
        catch (JsonException ex)
        {
            return PlanValidationResult.Fail($"Plan JSON is invalid: {ex.Message}");
        }

        if (planNode is not JsonObject planObj)
        {
            return PlanValidationResult.Fail("Plan JSON must be an object.");
        }

        foreach (var property in planObj)
        {
            if (!AllowedFields.Contains(property.Key, StringComparer.OrdinalIgnoreCase))
            {
                return PlanValidationResult.Fail($"Unknown field '{property.Key}' in plan.");
            }
        }

        var datasetKey = planObj["datasetKey"]?.GetValue<string>()?.Trim();
        if (string.IsNullOrWhiteSpace(datasetKey))
        {
            return PlanValidationResult.Fail("datasetKey is required.");
        }

        planObj["datasetKey"] = datasetKey;

        var selectNode = planObj["select"];
        if (selectNode is not JsonArray selectArray || selectArray.Count == 0)
        {
            return PlanValidationResult.Fail("select must be a non-empty array.");
        }

        var datasets = await _catalogProvider.GetDatasetsAsync(context.TenantId, ct);
        var dataset = datasets.FirstOrDefault(item => item.IsEnabled
            && string.Equals(item.DatasetKey, datasetKey, StringComparison.OrdinalIgnoreCase));
        if (dataset is null)
        {
            return PlanValidationResult.Fail($"Dataset '{datasetKey}' is not available.");
        }

        var fields = await _catalogProvider.GetFieldsAsync(context.TenantId, datasetKey, ct);
        var fieldLookup = new HashSet<string>(fields.Where(field => field.IsEnabled).Select(field => field.FieldKey), StringComparer.OrdinalIgnoreCase);

        var normalizedSelect = new JsonArray();
        foreach (var entry in selectArray)
        {
            var fieldKey = entry?.GetValue<string>()?.Trim();
            if (string.IsNullOrWhiteSpace(fieldKey))
            {
                return PlanValidationResult.Fail("select entries must be field keys.");
            }

            if (!fieldLookup.Contains(fieldKey))
            {
                return PlanValidationResult.Fail($"Unknown field '{fieldKey}' in select.");
            }

            normalizedSelect.Add(fieldKey);
        }

        planObj["select"] = normalizedSelect;

        if (!ValidateFieldArray(planObj["groupBy"], "groupBy", fieldLookup, out var normalizedGroupBy, out var groupError))
        {
            return PlanValidationResult.Fail(groupError);
        }
        if (normalizedGroupBy is not null)
        {
            planObj["groupBy"] = normalizedGroupBy;
        }

        if (!ValidateOrderBy(planObj["orderBy"], fieldLookup, out var normalizedOrderBy, out var orderError))
        {
            return PlanValidationResult.Fail(orderError);
        }
        if (normalizedOrderBy is not null)
        {
            planObj["orderBy"] = normalizedOrderBy;
        }

        if (!ValidateWhere(planObj["where"], fieldLookup, out var whereError))
        {
            return PlanValidationResult.Fail(whereError);
        }

        if (!ValidateTimeRange(planObj["timeRange"], out var timeError))
        {
            return PlanValidationResult.Fail(timeError);
        }

        if (!ValidateLimit(planObj["limit"], out var limitError))
        {
            return PlanValidationResult.Fail(limitError);
        }

        if (!ValidateOffset(planObj["offset"], out var offsetError))
        {
            return PlanValidationResult.Fail(offsetError);
        }

        var drilldownValidation = await ValidateDrilldownAsync(plan: planObj["drilldown"], context, datasetKey, ct);
        if (!drilldownValidation.IsValid)
        {
            return PlanValidationResult.Fail(drilldownValidation.Error ?? "drilldown validation failed.");
        }

        using var normalizedDoc = JsonDocument.Parse(planObj.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
        return PlanValidationResult.Success(normalizedDoc);
    }

    private bool ValidateLimit(JsonNode? limitNode, out string error)
    {
        error = string.Empty;
        if (limitNode is null)
        {
            return true;
        }

        if (limitNode is not JsonValue limitValue || !limitValue.TryGetValue<int>(out var limit))
        {
            error = "limit must be an integer.";
            return false;
        }

        if (limit <= 0 || limit > _atomicOptions.MaxLimit)
        {
            error = $"limit must be between 1 and {_atomicOptions.MaxLimit}.";
            return false;
        }

        return true;
    }

    private static bool ValidateOffset(JsonNode? offsetNode, out string error)
    {
        error = string.Empty;
        if (offsetNode is null)
        {
            return true;
        }

        if (offsetNode is not JsonValue offsetValue || !offsetValue.TryGetValue<int>(out var offset))
        {
            error = "offset must be an integer.";
            return false;
        }

        if (offset < 0)
        {
            error = "offset must be >= 0.";
            return false;
        }

        return true;
    }

    private bool ValidateTimeRange(JsonNode? timeRangeNode, out string error)
    {
        error = string.Empty;
        if (timeRangeNode is null)
        {
            return true;
        }

        if (timeRangeNode is not JsonObject timeRangeObj)
        {
            error = "timeRange must be an object.";
            return false;
        }

        var fromText = timeRangeObj["from"]?.GetValue<string>();
        var toText = timeRangeObj["to"]?.GetValue<string>();

        if (!DateTimeOffset.TryParse(fromText, out var from) || !DateTimeOffset.TryParse(toText, out var to))
        {
            error = "timeRange.from and timeRange.to must be valid dates.";
            return false;
        }

        if (to < from)
        {
            error = "timeRange.to must be after timeRange.from.";
            return false;
        }

        var days = (to - from).TotalDays;
        if (days > _atomicOptions.MaxTimeRangeDays)
        {
            error = $"timeRange exceeds max days {_atomicOptions.MaxTimeRangeDays}.";
            return false;
        }

        return true;
    }

    private static bool ValidateFieldArray(
        JsonNode? node,
        string fieldName,
        HashSet<string> fieldLookup,
        out JsonArray? normalized,
        out string error)
    {
        error = string.Empty;
        normalized = null;

        if (node is null)
        {
            return true;
        }

        if (node is not JsonArray array)
        {
            error = $"{fieldName} must be an array.";
            return false;
        }

        normalized = new JsonArray();
        foreach (var entry in array)
        {
            var fieldKey = entry?.GetValue<string>()?.Trim();
            if (string.IsNullOrWhiteSpace(fieldKey))
            {
                error = $"{fieldName} entries must be field keys.";
                return false;
            }

            if (!fieldLookup.Contains(fieldKey))
            {
                error = $"Unknown field '{fieldKey}' in {fieldName}.";
                return false;
            }

            normalized.Add(fieldKey);
        }

        return true;
    }

    private static bool ValidateWhere(JsonNode? node, HashSet<string> fieldLookup, out string error)
    {
        error = string.Empty;
        if (node is null)
        {
            return true;
        }

        if (node is not JsonArray array)
        {
            error = "where must be an array.";
            return false;
        }

        foreach (var entry in array)
        {
            if (entry is not JsonObject obj)
            {
                error = "where entries must be objects.";
                return false;
            }

            var fieldKey = obj["field"]?.GetValue<string>()?.Trim();
            if (string.IsNullOrWhiteSpace(fieldKey) || !fieldLookup.Contains(fieldKey))
            {
                error = $"Unknown field '{fieldKey}' in where clause.";
                return false;
            }

            var op = obj["op"]?.GetValue<string>()?.Trim();
            if (string.IsNullOrWhiteSpace(op) || !AllowedWhereOps.Contains(op))
            {
                error = $"Invalid operator '{op}' in where clause.";
                return false;
            }

            if (string.Equals(op, "in", StringComparison.OrdinalIgnoreCase)
                || string.Equals(op, "between", StringComparison.OrdinalIgnoreCase))
            {
                if (obj["values"] is not JsonArray values || values.Count == 0)
                {
                    error = $"where.values is required for operator '{op}'.";
                    return false;
                }

                if (string.Equals(op, "between", StringComparison.OrdinalIgnoreCase) && values.Count != 2)
                {
                    error = "where.values must contain exactly 2 values for between.";
                    return false;
                }
            }
            else
            {
                if (!obj.TryGetPropertyValue("value", out var valueNode) || valueNode is null)
                {
                    error = $"where.value is required for operator '{op}'.";
                    return false;
                }
            }
        }

        return true;
    }

    private async Task<(bool IsValid, string? Error)> ValidateDrilldownAsync(
        JsonNode? plan,
        TilsoftExecutionContext context,
        string datasetKey,
        CancellationToken ct)
    {
        if (plan is null)
        {
            return (true, null);
        }

        if (plan is not JsonObject drilldownObj)
        {
            return (false, "drilldown must be an object.");
        }

        var toDatasetKey = drilldownObj["toDatasetKey"]?.GetValue<string>()?.Trim();
        var joinKey = drilldownObj["joinKey"]?.GetValue<string>()?.Trim();

        if (string.IsNullOrWhiteSpace(toDatasetKey) || string.IsNullOrWhiteSpace(joinKey))
        {
            return (false, "drilldown must include toDatasetKey and joinKey.");
        }

        var datasets = await _catalogProvider.GetDatasetsAsync(context.TenantId, ct);
        var targetDataset = datasets.FirstOrDefault(item => item.IsEnabled
            && string.Equals(item.DatasetKey, toDatasetKey, StringComparison.OrdinalIgnoreCase));
        if (targetDataset is null)
        {
            return (false, $"Dataset '{toDatasetKey}' is not available.");
        }

        var graphs = await _catalogProvider.GetEntityGraphsAsync(context.TenantId, ct);
        var graph = graphs.FirstOrDefault(item =>
            item.IsEnabled && string.Equals(item.GraphKey, joinKey, StringComparison.OrdinalIgnoreCase));

        if (graph is null)
        {
            return (false, $"Join graph '{joinKey}' is not available.");
        }

        var connects = (string.Equals(graph.FromDatasetKey, datasetKey, StringComparison.OrdinalIgnoreCase)
            && string.Equals(graph.ToDatasetKey, toDatasetKey, StringComparison.OrdinalIgnoreCase))
            || (string.Equals(graph.FromDatasetKey, toDatasetKey, StringComparison.OrdinalIgnoreCase)
                && string.Equals(graph.ToDatasetKey, datasetKey, StringComparison.OrdinalIgnoreCase));

        if (!connects)
        {
            return (false, $"Join graph '{joinKey}' does not connect dataset '{datasetKey}' to '{toDatasetKey}'.");
        }

        if (_atomicOptions.MaxJoins < 1)
        {
            return (false, "Joins are not permitted by policy.");
        }

        var targetFields = await _catalogProvider.GetFieldsAsync(context.TenantId, toDatasetKey, ct);
        var targetFieldLookup = new HashSet<string>(
            targetFields.Where(field => field.IsEnabled).Select(field => field.FieldKey),
            StringComparer.OrdinalIgnoreCase);

        if (!ValidateWhere(drilldownObj["where"], targetFieldLookup, out var whereError))
        {
            return (false, whereError);
        }

        drilldownObj["toDatasetKey"] = toDatasetKey;
        drilldownObj["joinKey"] = joinKey;
        return (true, null);
    }

    private static bool ValidateOrderBy(
        JsonNode? node,
        HashSet<string> fieldLookup,
        out JsonArray? normalized,
        out string error)
    {
        error = string.Empty;
        normalized = null;

        if (node is null)
        {
            return true;
        }

        if (node is not JsonArray array)
        {
            error = "orderBy must be an array.";
            return false;
        }

        normalized = new JsonArray();
        foreach (var entry in array)
        {
            if (entry is not JsonObject obj)
            {
                error = "orderBy entries must be objects.";
                return false;
            }

            var fieldKey = obj["field"]?.GetValue<string>()?.Trim();
            if (string.IsNullOrWhiteSpace(fieldKey) || !fieldLookup.Contains(fieldKey))
            {
                error = $"Unknown field '{fieldKey}' in orderBy.";
                return false;
            }

            var dir = obj["dir"]?.GetValue<string>()?.Trim().ToLowerInvariant();
            if (dir is not "asc" and not "desc")
            {
                error = "orderBy.dir must be 'asc' or 'desc'.";
                return false;
            }

            normalized.Add(new JsonObject
            {
                ["field"] = fieldKey,
                ["dir"] = dir
            });
        }

        return true;
    }

    private static readonly HashSet<string> AllowedWhereOps = new(StringComparer.OrdinalIgnoreCase)
    {
        "eq",
        "ne",
        "gt",
        "gte",
        "lt",
        "lte",
        "like",
        "in",
        "between"
    };

    private const string AtomicPlanSchema = """
{
  "type": "object",
  "required": ["datasetKey", "select"],
  "additionalProperties": false,
  "properties": {
    "datasetKey": { "type": "string" },
    "select": { "type": "array" },
    "where": { "type": "array" },
    "groupBy": { "type": "array" },
    "orderBy": { "type": "array", "items": { "type": "object", "required": ["field","dir"], "properties": { "field": { "type": "string" }, "dir": { "type": "string" } }, "additionalProperties": false } },
    "limit": { "type": "integer" },
    "offset": { "type": "integer" },
    "timeRange": { "type": "object" },
    "drilldown": { "type": "object" }
  }
}
""";
}
