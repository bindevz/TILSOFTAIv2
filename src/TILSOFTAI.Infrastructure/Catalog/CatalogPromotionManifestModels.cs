namespace TILSOFTAI.Infrastructure.Catalog;

public static class CatalogPromotionManifestStatus
{
    public const string Issued = "issued";
}

public static class CatalogRolloutAttestationStates
{
    public const string Issued = "issued";
    public const string Started = "started";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Aborted = "aborted";
    public const string Superseded = "superseded";
}

public sealed class CatalogPromotionManifestIssueRequest
{
    public string EnvironmentName { get; init; } = string.Empty;
    public IReadOnlyList<string> ChangeIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> EvidenceIds { get; init; } = Array.Empty<string>();
    public string RollbackOfManifestId { get; init; } = string.Empty;
    public string RelatedIncidentId { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
}

public sealed class CatalogPromotionManifestIssueResult
{
    public bool IsIssued { get; init; }
    public CatalogPromotionManifestRecord? Manifest { get; init; }
    public IReadOnlyList<string> Blockers { get; init; } = Array.Empty<string>();
    public IReadOnlyList<CatalogPromotionGateResult> GateResults { get; init; } = Array.Empty<CatalogPromotionGateResult>();
}

public sealed record CatalogPromotionManifestRecord
{
    public string ManifestId { get; init; } = string.Empty;
    public string ManifestHash { get; init; } = string.Empty;
    public string EnvironmentName { get; init; } = string.Empty;
    public string Status { get; init; } = CatalogPromotionManifestStatus.Issued;
    public IReadOnlyList<string> ChangeIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> EvidenceIds { get; init; } = Array.Empty<string>();
    public string GateSummaryJson { get; init; } = "{}";
    public string RollbackOfManifestId { get; init; } = string.Empty;
    public string RelatedIncidentId { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
    public string CreatedByUserId { get; init; } = string.Empty;
    public string IssuedByUserId { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime IssuedAtUtc { get; init; } = DateTime.UtcNow;
}

public sealed class CatalogRolloutAttestationRequest
{
    public string State { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
    public IReadOnlyList<string> EvidenceIds { get; init; } = Array.Empty<string>();
    public string AcceptedByUserId { get; init; } = string.Empty;
}

public sealed class CatalogRolloutAttestationResult
{
    public bool IsRecorded { get; init; }
    public CatalogRolloutAttestationRecord? Attestation { get; init; }
    public IReadOnlyList<string> Blockers { get; init; } = Array.Empty<string>();
}

public sealed class CatalogDossierArchiveResult
{
    public bool IsArchived { get; init; }
    public CatalogDossierArchiveRecord? Archive { get; init; }
    public IReadOnlyList<string> Blockers { get; init; } = Array.Empty<string>();
}

public sealed class CatalogDossierArchiveVerificationResult
{
    public bool IsVerified { get; init; }
    public string ManifestId { get; init; } = string.Empty;
    public string DossierHash { get; init; } = string.Empty;
    public string ArchiveHash { get; init; } = string.Empty;
    public string ComputedArchiveHash { get; init; } = string.Empty;
    public string BackendName { get; init; } = string.Empty;
    public string StorageUri { get; init; } = string.Empty;
    public string PolicyVersion { get; init; } = string.Empty;
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}

public sealed class CatalogRolloutAttestationRecord
{
    public string AttestationId { get; init; } = string.Empty;
    public string ManifestId { get; init; } = string.Empty;
    public string EnvironmentName { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
    public IReadOnlyList<string> EvidenceIds { get; init; } = Array.Empty<string>();
    public string ActorUserId { get; init; } = string.Empty;
    public string AcceptedByUserId { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}

public sealed record CatalogPromotionDossier
{
    public CatalogPromotionManifestRecord Manifest { get; init; } = new();
    public IReadOnlyList<CatalogChangeRequestRecord> Changes { get; init; } = Array.Empty<CatalogChangeRequestRecord>();
    public IReadOnlyList<CatalogCertificationEvidenceRecord> Evidence { get; init; } = Array.Empty<CatalogCertificationEvidenceRecord>();
    public IReadOnlyList<CatalogEvidenceTrustEvaluation> EvidenceTrust { get; init; } = Array.Empty<CatalogEvidenceTrustEvaluation>();
    public IReadOnlyList<CatalogRolloutAttestationRecord> Attestations { get; init; } = Array.Empty<CatalogRolloutAttestationRecord>();
    public CatalogAuditRetentionSnapshot Retention { get; init; } = new();
    public CatalogDossierArchiveRecord? Archive { get; init; }
    public CatalogDossierArchiveVerificationResult? ArchiveVerification { get; init; }
    public string DossierHash { get; init; } = string.Empty;
    public IReadOnlyList<string> AuditWarnings { get; init; } = Array.Empty<string>();
    public DateTime GeneratedAtUtc { get; init; } = DateTime.UtcNow;
}

public sealed record CatalogDossierArchiveRecord
{
    public string ManifestId { get; init; } = string.Empty;
    public string DossierHash { get; init; } = string.Empty;
    public string ArchiveHash { get; init; } = string.Empty;
    public string ArchivePath { get; init; } = string.Empty;
    public string BackendName { get; init; } = string.Empty;
    public string StorageUri { get; init; } = string.Empty;
    public string SealAlgorithm { get; init; } = "sha256";
    public string PolicyVersion { get; init; } = string.Empty;
    public string CreatedByUserId { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime? RetainUntilUtc { get; init; }
}

public sealed class CatalogAuditRetentionSnapshot
{
    public string PolicyVersion { get; init; } = "sprint-15";
    public DateTime? ManifestRetainUntilUtc { get; init; }
    public DateTime? EvidenceRetainUntilUtc { get; init; }
    public DateTime? AttestationRetainUntilUtc { get; init; }
    public DateTime? DossierArchiveRetainUntilUtc { get; init; }
    public bool ArchiveRequired { get; init; }
    public bool RetentionCurrent { get; init; }
}
