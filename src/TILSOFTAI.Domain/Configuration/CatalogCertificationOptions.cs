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
    public string MinimumEvidenceTrustTierForProductionLikePromotion { get; set; } = "provider_verified";
    public Dictionary<string, string> EnvironmentMinimumEvidenceTrustTiers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> EvidenceFreshnessDaysByKind { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string TrustedArtifactRootPath { get; set; } = "evidence-artifacts";
    public string[] ControlledArtifactUriPrefixes { get; set; } = { "artifact://catalog-evidence/" };
    public int EvidenceRetentionDays { get; set; } = 2555;
    public int ManifestRetentionDays { get; set; } = 2555;
    public int AttestationRetentionDays { get; set; } = 2555;
    public int DossierArchiveRetentionDays { get; set; } = 2555;
    public bool RequireArchiveForProductionLikeDossiers { get; set; } = true;
    public string[] TrustedEvidenceStatuses { get; set; } = { "accepted" };
    public string[] AllowedEvidenceUriPrefixes { get; set; } = { "https://evidence.example/", "artifact://catalog-evidence/" };
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
