using TILSOFTAI.Orchestration.Normalization;
using TILSOFTAI.Orchestration.Sql;

namespace TILSOFTAI.Infrastructure.Normalization;

public sealed class SqlNormalizationRuleProvider : INormalizationRuleProvider
{
    private readonly ISqlExecutor _sqlExecutor;

    public SqlNormalizationRuleProvider(ISqlExecutor sqlExecutor)
    {
        _sqlExecutor = sqlExecutor ?? throw new ArgumentNullException(nameof(sqlExecutor));
    }

    public async Task<IReadOnlyList<NormalizationRuleRecord>> GetRulesAsync(string tenantId, CancellationToken ct)
    {
        var rows = await _sqlExecutor.ExecuteQueryAsync(
            "dbo.app_normalizationrule_list",
            new Dictionary<string, object?>
            {
                ["@TenantId"] = tenantId
            },
            ct);

        var rules = new List<NormalizationRuleRecord>();
        foreach (var row in rows)
        {
            if (!TryGetString(row, "RuleKey", out var ruleKey) || string.IsNullOrWhiteSpace(ruleKey))
            {
                continue;
            }

            if (!TryGetString(row, "Pattern", out var pattern) || string.IsNullOrWhiteSpace(pattern))
            {
                continue;
            }

            if (!TryGetString(row, "Replacement", out var replacement))
            {
                replacement = string.Empty;
            }

            var priority = TryGetInt(row, "Priority");
            rules.Add(new NormalizationRuleRecord(ruleKey, priority, pattern, replacement ?? string.Empty));
        }

        return rules;
    }

    private static bool TryGetString(IReadOnlyDictionary<string, object?> row, string key, out string? value)
    {
        value = null;
        if (!row.TryGetValue(key, out var raw) || raw is null)
        {
            return false;
        }

        value = Convert.ToString(raw);
        return !string.IsNullOrWhiteSpace(value);
    }

    private static int TryGetInt(IReadOnlyDictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var raw) || raw is null)
        {
            return 0;
        }

        return Convert.ToInt32(raw);
    }
}
