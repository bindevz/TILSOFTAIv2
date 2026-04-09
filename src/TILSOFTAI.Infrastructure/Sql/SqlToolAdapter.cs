using System.Text.Json;
using TILSOFTAI.Orchestration.Sql;
using TILSOFTAI.Tools.Abstractions;

namespace TILSOFTAI.Infrastructure.Sql;

public sealed class SqlToolAdapter : IToolAdapter
{
    private readonly ISqlExecutor _sqlExecutor;

    public SqlToolAdapter(ISqlExecutor sqlExecutor)
    {
        _sqlExecutor = sqlExecutor ?? throw new ArgumentNullException(nameof(sqlExecutor));
    }

    public string AdapterType => "sql";

    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var storedProcedure = GetRequiredMetadata(request, "storedProcedure");
        return request.Operation switch
        {
            ToolAdapterOperationNames.ExecuteTool => ToolExecutionResult.Ok(
                await _sqlExecutor.ExecuteToolAsync(storedProcedure, request.TenantId, request.ArgumentsJson, ct)),

            ToolAdapterOperationNames.ExecuteWriteAction => ToolExecutionResult.Ok(
                await _sqlExecutor.ExecuteWriteActionAsync(storedProcedure, request.TenantId, request.ArgumentsJson, ct)),

            ToolAdapterOperationNames.ExecuteAtomicPlan => ToolExecutionResult.Ok(
                await _sqlExecutor.ExecuteAtomicPlanAsync(
                    storedProcedure,
                    request.TenantId,
                    request.ArgumentsJson,
                    request.AgentId,
                    string.Join(",", ReadRoles(request)),
                    ct)),

            ToolAdapterOperationNames.ExecuteDiagnostics => ToolExecutionResult.Ok(
                await ExecuteDiagnosticsAsync(request, storedProcedure, ct)),

            ToolAdapterOperationNames.ExecuteScalar => ToolExecutionResult.Ok(
                await _sqlExecutor.ExecuteAsync(storedProcedure, ReadParameterMap(request.ArgumentsJson), ct)),

            ToolAdapterOperationNames.ExecuteQuery => await ExecuteQueryAsync(request, storedProcedure, ct),

            _ => ToolExecutionResult.Fail("SQL_OPERATION_NOT_SUPPORTED", new { request.Operation })
        };
    }

    private async Task<ToolExecutionResult> ExecuteQueryAsync(
        ToolExecutionRequest request,
        string storedProcedure,
        CancellationToken ct)
    {
        var rows = await _sqlExecutor.ExecuteQueryAsync(storedProcedure, ReadParameterMap(request.ArgumentsJson), ct);
        return ToolExecutionResult.Ok(JsonSerializer.Serialize(rows), rows);
    }

    private async Task<string> ExecuteDiagnosticsAsync(
        ToolExecutionRequest request,
        string storedProcedure,
        CancellationToken ct)
    {
        using var document = JsonDocument.Parse(request.ArgumentsJson);
        var root = document.RootElement;

        var module = root.TryGetProperty("module", out var moduleNode) ? moduleNode.GetString() ?? string.Empty : string.Empty;
        var ruleKey = root.TryGetProperty("ruleKey", out var ruleKeyNode) ? ruleKeyNode.GetString() ?? string.Empty : string.Empty;
        var inputJson = root.TryGetProperty("inputJson", out var inputJsonNode)
            ? inputJsonNode.GetRawText()
            : null;

        return await _sqlExecutor.ExecuteDiagnosticsAsync(
            storedProcedure,
            request.TenantId,
            module,
            ruleKey,
            inputJson,
            ct);
    }

    private static IReadOnlyDictionary<string, object?> ReadParameterMap(string json)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in document.RootElement.EnumerateObject())
        {
            result[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Number when property.Value.TryGetInt64(out var longValue) => longValue,
                JsonValueKind.Number when property.Value.TryGetDecimal(out var decimalValue) => decimalValue,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => property.Value.GetRawText()
            };
        }

        return result;
    }

    private static IReadOnlyList<string> ReadRoles(ToolExecutionRequest request)
    {
        if (!request.Metadata.TryGetValue("roles", out var value) || string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string GetRequiredMetadata(ToolExecutionRequest request, string key)
    {
        if (request.Metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw new InvalidOperationException($"SQL tool adapter requires metadata value '{key}'.");
    }
}
