using System.Text.Json;
using Microsoft.Extensions.Logging;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Sql;
using TILSOFTAI.Orchestration.Tools;

namespace TILSOFTAI.Modules.Analytics.Tools;

/// <summary>
/// Handler for analytics_execute_plan tool - Execute validated analytics plans with metrics.
/// PATCH 29.01: Metrics Execution Engine
/// </summary>
public sealed class AnalyticsExecutePlanToolHandler : AnalyticsToolHandlerBase
{
    private const int MaxPayloadSize = 64 * 1024; // 64KB max payload
    
    public override string ToolName => "analytics_execute_plan";

    public AnalyticsExecutePlanToolHandler(
        ISqlExecutor sqlExecutor,
        IExecutionContextAccessor contextAccessor,
        ILogger<AnalyticsExecutePlanToolHandler> logger)
        : base(sqlExecutor, contextAccessor, logger)
    {
    }

    public override async Task<string> ExecuteAsync(
        ToolDefinition tool,
        string argumentsJson,
        TilsoftExecutionContext context,
        CancellationToken ct)
    {
        // Validate payload size
        if (!string.IsNullOrEmpty(argumentsJson) && argumentsJson.Length > MaxPayloadSize)
        {
            throw new ArgumentException($"Payload size exceeds maximum allowed ({MaxPayloadSize} bytes)");
        }

        // Parse to validate structure and get datasetKey for logging
        var args = ParseArguments<ExecutePlanArgs>(argumentsJson);
        
        if (args == null)
        {
            throw new ArgumentException("Invalid arguments for analytics_execute_plan");
        }

        var startTime = DateTime.UtcNow;
        var planHash = ComputePlanHash(argumentsJson);

        Logger.LogInformation(
            "AnalyticsExecutePlan | DatasetKey: {DatasetKey} | PlanHash: {PlanHash}",
            args.DatasetKey ?? "unknown",
            planHash);

        var parameters = new Dictionary<string, object?>
        {
            ["TenantId"] = context.TenantId,
            ["ArgsJson"] = argumentsJson
        };

        var result = await ExecuteSpAsync("dbo.ai_analytics_execute_plan", parameters, ct);

        var duration = DateTime.UtcNow - startTime;
        Logger.LogInformation(
            "AnalyticsExecutePlanComplete | DatasetKey: {DatasetKey} | PlanHash: {PlanHash} | DurationMs: {DurationMs}",
            args.DatasetKey ?? "unknown",
            planHash,
            duration.TotalMilliseconds);

        return result;
    }

    private static string ComputePlanHash(string json)
    {
        if (string.IsNullOrEmpty(json))
            return "empty";

        // Simple hash for correlation/logging (not cryptographic)
        var hash = 0;
        foreach (var c in json)
        {
            hash = ((hash << 5) - hash) + c;
            hash &= 0x7FFFFFFF; // Keep positive
        }
        return hash.ToString("X8");
    }

    private sealed class ExecutePlanArgs
    {
        public string? DatasetKey { get; set; }
        public object[]? Metrics { get; set; }
        public string[]? GroupBy { get; set; }
        public object[]? Where { get; set; }
        public object[]? OrderBy { get; set; }
        public int? Limit { get; set; }
    }
}
