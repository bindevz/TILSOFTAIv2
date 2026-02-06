using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Sql;
using TILSOFTAI.Orchestration.Tools;

namespace TILSOFTAI.Modules.Analytics.Tools;

/// <summary>
/// Handler for analytics_validate_plan tool - Validate atomic plan before execution.
/// PATCH 30.02: Server-trusted roles injection (strip LLM-provided _roles).
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

        // PATCH 30.02: Strip LLM-provided _roles and inject server-trusted roles
        var securedPlanJson = InjectServerTrustedRoles(planJsonString, context);
        
        // Log with rolesHash only (not raw roles) for security
        var rolesHash = ComputeRolesHash(context.Roles);
        Logger.LogInformation(
            "AnalyticsValidatePlan.ServerTrustedRoles | CorrelationId: {CorrelationId} | RolesHash: {RolesHash}",
            context.CorrelationId, rolesHash);

        var parameters = new Dictionary<string, object?>
        {
            ["TenantId"] = context.TenantId,
            ["PlanJson"] = securedPlanJson
        };

        return await ExecuteSpAsync("dbo.ai_analytics_validate_plan", parameters, ct);
    }

    /// <summary>
    /// PATCH 30.02: Strip any _roles from plan JSON and inject server-trusted roles.
    /// This prevents privilege escalation by LLM injecting roles.
    /// </summary>
    private static string InjectServerTrustedRoles(string planJson, TilsoftExecutionContext context)
    {
        try
        {
            var node = JsonNode.Parse(planJson);
            if (node is JsonObject obj)
            {
                // Strip any existing _roles (could be LLM-injected)
                obj.Remove("_roles");
                
                // Inject server-trusted roles (sorted, unique)
                var serverRoles = context.Roles?
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                    .Select(r => r.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(r => r, StringComparer.OrdinalIgnoreCase)
                    .ToArray() ?? Array.Empty<string>();
                
                obj["_roles"] = new JsonArray(serverRoles.Select(r => JsonValue.Create(r)).ToArray());
                
                // Optional: inject _caller metadata for auditing (no PII)
                obj["_caller"] = new JsonObject
                {
                    ["tenantId"] = context.TenantId,
                    ["userId"] = context.UserId, // Assumed to be non-PII identifier
                    ["correlationId"] = context.CorrelationId
                };
                
                return obj.ToJsonString();
            }
        }
        catch
        {
            // If parse fails, return original (SP will handle invalid JSON)
        }
        
        return planJson;
    }

    /// <summary>
    /// Compute SHA256 hash of roles for logging (truncated to 8 chars).
    /// </summary>
    private static string ComputeRolesHash(IEnumerable<string>? roles)
    {
        if (roles == null || !roles.Any())
            return "empty";
            
        var sorted = string.Join(",", roles.OrderBy(r => r, StringComparer.OrdinalIgnoreCase));
        var bytes = Encoding.UTF8.GetBytes(sorted);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash)[..8].ToLowerInvariant();
    }

    private sealed class ValidatePlanArgs
    {
        public object? PlanJson { get; set; }
    }
}
