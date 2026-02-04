using System.Text.Json;
using Microsoft.Extensions.Logging;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Sql;
using TILSOFTAI.Orchestration.Tools;

namespace TILSOFTAI.Modules.Analytics.Tools;

/// <summary>
/// Base class for Analytics module tool handlers.
/// Provides common SP execution pattern.
/// </summary>
public abstract class AnalyticsToolHandlerBase : IToolHandler
{
    protected readonly ISqlExecutor SqlExecutor;
    protected readonly IExecutionContextAccessor ContextAccessor;
    protected readonly ILogger Logger;

    protected AnalyticsToolHandlerBase(
        ISqlExecutor sqlExecutor,
        IExecutionContextAccessor contextAccessor,
        ILogger logger)
    {
        SqlExecutor = sqlExecutor ?? throw new ArgumentNullException(nameof(sqlExecutor));
        ContextAccessor = contextAccessor ?? throw new ArgumentNullException(nameof(contextAccessor));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public abstract string ToolName { get; }

    public abstract Task<string> ExecuteAsync(
        ToolDefinition tool,
        string argumentsJson,
        TilsoftExecutionContext context,
        CancellationToken ct);

    protected async Task<string> ExecuteSpAsync(
        string spName,
        Dictionary<string, object?> parameters,
        CancellationToken ct)
    {
        var context = ContextAccessor.Current;
        
        // Always inject TenantId from context
        if (!parameters.ContainsKey("TenantId"))
        {
            parameters["TenantId"] = context?.TenantId ?? throw new InvalidOperationException("TenantId is required");
        }

        Logger.LogDebug(
            "ExecutingSP | SpName: {SpName} | ParamCount: {ParamCount}",
            spName, parameters.Count);

        var result = await SqlExecutor.ExecuteAsync(spName, parameters, ct);
        
        Logger.LogDebug(
            "SPExecuted | SpName: {SpName} | ResultLength: {Length}",
            spName, result?.Length ?? 0);

        return result ?? "{}";
    }

    protected static T? ParseArguments<T>(string argumentsJson) where T : class
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
            return null;

        return JsonSerializer.Deserialize<T>(argumentsJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }
}
