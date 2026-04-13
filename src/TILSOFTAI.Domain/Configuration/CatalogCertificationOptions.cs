namespace TILSOFTAI.Domain.Configuration;

public sealed class CatalogCertificationOptions
{
    public string EnvironmentName { get; set; } = "development";
    public string[] ProductionLikeEnvironments { get; set; } = { "prod", "production", "staging" };
    public bool RequireCertificationEvidenceForProductionLikePromotion { get; set; } = true;
    public bool RequireTrustedEvidenceForProductionLikePromotion { get; set; } = true;
    public bool RequireArtifactHashForTrustedEvidence { get; set; } = true;
    public bool RequireEvidenceUriForTrustedEvidence { get; set; } = true;
    public bool RequireRolloutAttestationEvidenceForProductionLikeCompletion { get; set; } = true;
    public int MaxTrustedEvidenceAgeDays { get; set; } = 90;
    public string[] TrustedEvidenceStatuses { get; set; } = { "accepted" };
    public string[] AllowedEvidenceUriPrefixes { get; set; } = { "https://evidence.example/" };
    public string[] AllowedEvidenceContentTypes { get; set; } =
    {
        "application/json",
        "application/pdf",
        "text/plain",
        "text/markdown"
    };
    public string[] AllowedEvidenceSourceSystems { get; set; } =
    {
        "ci",
        "runbook",
        "incident",
        "release"
    };
    public string[] RequiredEvidenceKinds { get; set; } =
    {
        "runbook_execution",
        "preview_failure_drill",
        "version_conflict_drill",
        "duplicate_submit_drill",
        "sql_apply_outage_drill",
        "fallback_risk_drill",
        "operator_signoff"
    };

    public int PreviewSuccessSloPercent { get; set; } = 99;
    public int SubmitSuccessSloPercent { get; set; } = 99;
    public int ApproveSuccessSloPercent { get; set; } = 99;
    public int ApplySuccessSloPercent { get; set; } = 99;
    public int RollbackReadyMinutes { get; set; } = 30;
    public int VersionConflictAlertThresholdPerHour { get; set; } = 3;
    public int DuplicateSubmitAlertThresholdPerHour { get; set; } = 5;
    public int ApplyFailureAlertThresholdPerHour { get; set; } = 1;
    public int RollbackSurgeAlertThresholdPerHour { get; set; } = 2;
}
