using System.Text.Json;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Sql;

namespace TILSOFTAI.Modules.Model.Tools;

public abstract class ModelToolHandlerBase
{
    private readonly ISqlExecutor _sqlExecutor;

    protected ModelToolHandlerBase(ISqlExecutor sqlExecutor)
    {
        _sqlExecutor = sqlExecutor ?? throw new ArgumentNullException(nameof(sqlExecutor));
    }

    protected static JsonDocument ParseArguments(string argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            throw new ArgumentException("Arguments JSON is required.", nameof(argumentsJson));
        }

        return JsonDocument.Parse(argumentsJson);
    }

    protected static int RequireInt(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element))
        {
            throw new ArgumentException($"Missing required property '{propertyName}'.", nameof(propertyName));
        }

        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out var value) || value < 1)
        {
            throw new ArgumentException($"Property '{propertyName}' must be an integer >= 1.", nameof(propertyName));
        }

        return value;
    }

    protected static IReadOnlyList<int> RequireIntArray(JsonElement root, string propertyName, int minimumCount)
    {
        if (!root.TryGetProperty(propertyName, out var element))
        {
            throw new ArgumentException($"Missing required property '{propertyName}'.", nameof(propertyName));
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException($"Property '{propertyName}' must be an array.", nameof(propertyName));
        }

        var values = new List<int>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Number || !item.TryGetInt32(out var value) || value < 1)
            {
                throw new ArgumentException($"Property '{propertyName}' must contain integers >= 1.", nameof(propertyName));
            }

            values.Add(value);
        }

        if (values.Count < minimumCount)
        {
            throw new ArgumentException($"Property '{propertyName}' must contain at least {minimumCount} items.", nameof(propertyName));
        }

        return values;
    }

    protected Task<string> ExecuteAsync(
        string storedProcedure,
        TilsoftExecutionContext context,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storedProcedure))
        {
            throw new ArgumentException("Stored procedure name is required.", nameof(storedProcedure));
        }

        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (string.IsNullOrWhiteSpace(context.TenantId))
        {
            throw new InvalidOperationException("Execution context TenantId is required.");
        }

        // Patch 26.01: Model procedures only accept @TenantId and @ArgsJson
        // Removed @Language parameter to match new signature
        var sqlParams = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["@TenantId"] = context.TenantId
        };

        foreach (var (key, value) in parameters)
        {
            sqlParams[key] = value ?? DBNull.Value;
        }

        return _sqlExecutor.ExecuteAsync(storedProcedure, sqlParams, cancellationToken);
    }
}
