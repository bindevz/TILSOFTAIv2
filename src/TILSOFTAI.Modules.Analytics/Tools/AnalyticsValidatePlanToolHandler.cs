using System.Text.Json;
using Microsoft.Extensions.Logging;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Sql;
using TILSOFTAI.Orchestration.Tools;

namespace TILSOFTAI.Modules.Analytics.Tools;

/// <summary>
/// Handler for analytics_validate_plan tool - Validate atomic plan before execution.
/// </summary>
public sealed class AnalyticsValidatePlanToolHandler : AnalyticsToolHandlerBase
{
    public override string ToolName => "analytics_validate_plan";

    public AnalyticsValidatePlanToolHandler(
        ISqlExecutor sqlExecutor,
        IExecutionContextAccessor contextAccessor,
        ILogger<AnalyticsValidatePlanToolHandler> logger)
        : base(sqlExecutor, contextAccessor, logger)
    {
    }

    public override async Task<string> ExecuteAsync(
        ToolDefinition tool,
        string argumentsJson,
        TilsoftExecutionContext context,
        CancellationToken ct)
    {
        var args = ParseArguments<ValidatePlanArgs>(argumentsJson);
        
        if (args?.PlanJson == null)
        {
            throw new ArgumentException("PlanJson is required for analytics_validate_plan");
        }

        // Serialize planJson object to string for SP
        var planJsonString = args.PlanJson is JsonElement element
            ? element.GetRawText()
            : JsonSerializer.Serialize(args.PlanJson);

        var parameters = new Dictionary<string, object?>
        {
            ["TenantId"] = context.TenantId,
            ["PlanJson"] = planJsonString
        };

        return await ExecuteSpAsync("dbo.ai_analytics_validate_plan", parameters, ct);
    }

    private sealed class ValidatePlanArgs
    {
        public object? PlanJson { get; set; }
    }
}
