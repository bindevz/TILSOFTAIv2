namespace TILSOFTAI.Infrastructure.Catalog;

public static class CatalogCertificationEvidenceKinds
{
    public const string RunbookExecution = "runbook_execution";
    public const string PreviewFailureDrill = "preview_failure_drill";
    public const string VersionConflictDrill = "version_conflict_drill";
    public const string DuplicateSubmitDrill = "duplicate_submit_drill";
    public const string SqlApplyOutageDrill = "sql_apply_outage_drill";
    public const string FallbackRiskDrill = "fallback_risk_drill";
    public const string OperatorSignoff = "operator_signoff";
}

public static class CatalogCertificationEvidenceStatus
{
    public const string Accepted = "accepted";
    public const string Pending = "pending";
    public const string Rejected = "rejected";
}

public sealed class CatalogCertificationEvidenceRecord
{
    public string EvidenceId { get; init; } = string.Empty;
    public string EnvironmentName { get; init; } = string.Empty;
    public string EvidenceKind { get; init; } = string.Empty;
    public string Status { get; init; } = CatalogCertificationEvidenceStatus.Pending;
    public string Summary { get; init; } = string.Empty;
    public string EvidenceUri { get; init; } = string.Empty;
    public string RelatedChangeId { get; init; } = string.Empty;
    public string RelatedIncidentId { get; init; } = string.Empty;
    public string OperatorUserId { get; init; } = string.Empty;
    public string ApprovedByUserId { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
    public DateTime CapturedAtUtc { get; init; } = DateTime.UtcNow;
}

public sealed class CatalogPromotionGateRequest
{
    public string EnvironmentName { get; init; } = string.Empty;
    public string ChangeId { get; init; } = string.Empty;
    public CatalogMutationRequest? MutationPreview { get; init; }
    public bool IncludeCertificationEvidence { get; init; } = true;
}

public sealed class CatalogPromotionGateResult
{
    public bool IsAllowed { get; init; }
    public string EnvironmentName { get; init; } = string.Empty;
    public string SourceMode { get; init; } = string.Empty;
    public bool ProductionLike { get; init; }
    public string ChangeId { get; init; } = string.Empty;
    public string RiskLevel { get; init; } = string.Empty;
    public IReadOnlyList<string> Blockers { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> EvidenceMissing { get; init; } = Array.Empty<string>();
    public CatalogMutationPreviewResult? Preview { get; init; }
}

public sealed class CatalogControlPlaneSloDefinition
{
    public string Name { get; init; } = string.Empty;
    public string Target { get; init; } = string.Empty;
    public string AlertCondition { get; init; } = string.Empty;
    public string Escalation { get; init; } = string.Empty;
}
