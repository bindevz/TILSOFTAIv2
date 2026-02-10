using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Orchestration.Policies;
using TILSOFTAI.Orchestration.Sql;

namespace TILSOFTAI.Infrastructure.Policies;

/// <summary>
/// SQL-backed ReAct follow-up rule provider with IMemoryCache.
/// Calls app_react_followup_list_scoped SP and caches by scope fingerprint.
/// </summary>
public sealed class SqlReActFollowUpRuleProvider : IReActFollowUpRuleProvider
{
    private readonly ISqlExecutor _sqlExecutor;
    private readonly IMemoryCache _cache;
    private readonly RuntimePolicySystemOptions _options;
    private readonly ILogger<SqlReActFollowUpRuleProvider> _logger;

    public SqlReActFollowUpRuleProvider(
        ISqlExecutor sqlExecutor,
        IMemoryCache cache,
        IOptions<RuntimePolicySystemOptions> options,
        ILogger<SqlReActFollowUpRuleProvider> logger)
    {
        _sqlExecutor = sqlExecutor ?? throw new ArgumentNullException(nameof(sqlExecutor));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<ReActFollowUpRule>> GetScopedRulesAsync(
        string tenantId,
        IReadOnlyList<string> moduleKeys,
        string? appKey = null,
        CancellationToken ct = default)
    {
        var cacheKey = $"followup_rules:{tenantId}:{string.Join(",", moduleKeys)}:{appKey}";

        if (_cache.TryGetValue<IReadOnlyList<ReActFollowUpRule>>(cacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var moduleKeysJson = JsonSerializer.Serialize(moduleKeys);
            var parameters = new Dictionary<string, object?>
            {
                ["@TenantId"] = tenantId,
                ["@ModuleKeysJson"] = moduleKeysJson,
                ["@AppKey"] = appKey
            };

            var rows = await _sqlExecutor.ExecuteQueryAsync("dbo.app_react_followup_list_scoped", parameters, ct);

            var rules = new List<ReActFollowUpRule>();

            foreach (var row in rows)
            {
                var rule = new ReActFollowUpRule(
                    RuleId: Convert.ToInt64(row.GetValueOrDefault("RuleId", 0L)),
                    RuleKey: row.GetValueOrDefault("RuleKey", "")?.ToString() ?? "",
                    ModuleKey: row.GetValueOrDefault("ModuleKey", "")?.ToString() ?? "",
                    ToolName: row.GetValueOrDefault("ToolName", null)?.ToString(),
                    Priority: Convert.ToInt32(row.GetValueOrDefault("Priority", 100)),
                    JsonPath: row.GetValueOrDefault("JsonPath", "")?.ToString() ?? "",
                    Operator: row.GetValueOrDefault("Operator", "")?.ToString() ?? "",
                    CompareValue: row.GetValueOrDefault("CompareValue", null)?.ToString(),
                    FollowUpToolName: row.GetValueOrDefault("FollowUpToolName", "")?.ToString() ?? "",
                    ArgsTemplateJson: row.GetValueOrDefault("ArgsTemplateJson", null)?.ToString(),
                    PromptHint: row.GetValueOrDefault("PromptHint", "")?.ToString() ?? "");

                rules.Add(rule);
            }

            _cache.Set(cacheKey, (IReadOnlyList<ReActFollowUpRule>)rules,
                TimeSpan.FromSeconds(_options.CacheTtlSeconds));

            _logger.LogDebug(
                "FollowUpRulesLoaded | TenantId: {TenantId} | Modules: [{Modules}] | RuleCount: {Count}",
                tenantId, string.Join(", ", moduleKeys), rules.Count);

            return rules;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load follow-up rules. Returning empty.");
            return Array.Empty<ReActFollowUpRule>();
        }
    }
}
