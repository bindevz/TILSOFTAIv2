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
/// Handler for analytics_execute_plan tool - Execute validated analytics plans with metrics.
/// PATCH 29.01: Metrics Execution Engine
/// PATCH 30.02: Server-trusted roles injection (strip LLM-provided _roles).
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

        // PATCH 30.02: Strip LLM-provided _roles and inject server-trusted roles
        var securedArgsJson = InjectServerTrustedRoles(argumentsJson, context);

        var startTime = DateTime.UtcNow;
        var planHash = ComputePlanHash(securedArgsJson);
        var rolesHash = ComputeRolesHash(context.Roles);

        Logger.LogInformation(
            "AnalyticsExecutePlan.ServerTrustedRoles | CorrelationId: {CorrelationId} | DatasetKey: {DatasetKey} | PlanHash: {PlanHash} | RolesHash: {RolesHash}",
            context.CorrelationId,
            args.DatasetKey ?? "unknown",
            planHash,
            rolesHash);

        var parameters = new Dictionary<string, object?>
        {
            ["TenantId"] = context.TenantId,
            ["ArgsJson"] = securedArgsJson  // Use secured JSON with server roles
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

    /// <summary>
    /// PATCH 30.02: Strip any _roles from args JSON and inject server-trusted roles.
    /// This prevents privilege escalation by LLM injecting roles.
    /// </summary>
    private static string InjectServerTrustedRoles(string argsJson, TilsoftExecutionContext context)
    {
        try
        {
            var node = JsonNode.Parse(argsJson);
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
                    ["userId"] = context.UserId,
                    ["correlationId"] = context.CorrelationId
                };
                
                return obj.ToJsonString();
            }
        }
        catch
        {
            // If parse fails, return original (SP will handle invalid JSON)
        }
        
        return argsJson;
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
