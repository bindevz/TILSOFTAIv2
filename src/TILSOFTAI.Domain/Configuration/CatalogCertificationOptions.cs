namespace TILSOFTAI.Domain.Configuration;

public sealed class CatalogCertificationOptions
{
    public string PolicyVersion { get; set; } = "sprint-16";
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
    public CatalogTrustedSignerOptions[] TrustedEvidenceSigners { get; set; } = Array.Empty<CatalogTrustedSignerOptions>();
    public string[] AllowedSignatureAlgorithms { get; set; } = { "RS256" };
    public string SignerTrustStorePath { get; set; } = "signer-trust-store.json";
    public bool RequireIndependentSignerTrustApproval { get; set; } = true;
    public string DossierArchiveRootPath { get; set; } = "dossier-archives";
    public string DossierArchiveBackend { get; set; } = "filesystem";
    public bool RequireArchivedDossierForProductionLikeCompletion { get; set; } = true;
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

public sealed class CatalogTrustedSignerOptions
{
    public string SignerId { get; set; } = string.Empty;
    public string KeyId { get; set; } = string.Empty;
    public string PublicKeyPem { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
    public DateTime? ValidFromUtc { get; set; }
    public DateTime? ValidUntilUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string RotatedToKeyId { get; set; } = string.Empty;
    public string TrustStoreVersion { get; set; } = "config";
    public string ApprovedChangeId { get; set; } = "config";
    public DateTime? LastChangedAtUtc { get; set; }
}
