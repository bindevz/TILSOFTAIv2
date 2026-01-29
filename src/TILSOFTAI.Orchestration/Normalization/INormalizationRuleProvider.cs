namespace TILSOFTAI.Orchestration.Normalization;

public interface INormalizationRuleProvider
{
    Task<IReadOnlyList<NormalizationRuleRecord>> GetRulesAsync(string tenantId, CancellationToken ct);
}
