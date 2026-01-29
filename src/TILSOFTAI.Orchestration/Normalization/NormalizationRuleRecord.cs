namespace TILSOFTAI.Orchestration.Normalization;

public sealed record NormalizationRuleRecord(string RuleKey, int Priority, string Pattern, string Replacement);
