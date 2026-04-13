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
    public const string Recorded = "recorded";
    public const string Verified = "verified";
    public const string Accepted = "accepted";
    public const string Expired = "expired";
    public const string Superseded = "superseded";
    public const string Pending = "pending";
    public const string Rejected = "rejected";
}

public static class CatalogEvidenceVerificationStatus
{
    public const string Unverified = "unverified";
    public const string Verified = "verified";
    public const string Failed = "failed";
    public const string Expired = "expired";
    public const string Superseded = "superseded";
}

public static class CatalogEvidenceTrustTiers
{
    public const string MetadataVerified = "metadata_verified";
    public const string ProviderVerified = "provider_verified";
    public const string SignatureVerified = "signature_verified";
    public const string ComplianceGradeTrusted = "compliance_grade_trusted";

    public static int Rank(string trustTier) =>
        trustTier?.Trim().ToLowerInvariant() switch
        {
            MetadataVerified => 1,
            ProviderVerified => 2,
            SignatureVerified => 3,
            ComplianceGradeTrusted => 4,
            _ => 0
        };
}

public static class CatalogEvidenceVerificationMethods
{
    public const string Metadata = "metadata";
    public const string Provider = "provider";
    public const string Signature = "signature";
}

public static class CatalogSignerLifecycleStates
{
    public const string Active = "active";
    public const string Rotated = "rotated";
    public const string Revoked = "revoked";
    public const string Retired = "retired";
}

public static class CatalogSignerTrustChangeOperations
{
    public const string AddSignerKey = "add_signer_key";
    public const string RotateSignerKey = "rotate_signer_key";
    public const string RevokeSignerKey = "revoke_signer_key";
    public const string RetireSignerKey = "retire_signer_key";
}

public static class CatalogSignerTrustChangeStatus
{
    public const string Proposed = "proposed";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Applied = "applied";
}

public sealed record CatalogTrustedSignerRecord
{
    public string SignerId { get; init; } = string.Empty;
    public string KeyId { get; init; } = string.Empty;
    public string PublicKeyPem { get; init; } = string.Empty;
    public string PublicKeyFingerprint { get; init; } = string.Empty;
    public string Status { get; init; } = CatalogSignerLifecycleStates.Active;
    public DateTime? ValidFromUtc { get; init; }
    public DateTime? ValidUntilUtc { get; init; }
    public DateTime? RevokedAtUtc { get; init; }
    public string RotatedToKeyId { get; init; } = string.Empty;
    public string TrustStoreVersion { get; init; } = string.Empty;
    public string ApprovedChangeId { get; init; } = string.Empty;
    public DateTime? LastChangedAtUtc { get; init; }
    public string Source { get; init; } = string.Empty;
}

public sealed class CatalogSignerTrustMutationRequest
{
    public string Operation { get; init; } = string.Empty;
    public string SignerId { get; init; } = string.Empty;
    public string KeyId { get; init; } = string.Empty;
    public string PublicKeyPem { get; init; } = string.Empty;
    public string RotatesFromKeyId { get; init; } = string.Empty;
    public DateTime? ValidFromUtc { get; init; }
    public DateTime? ValidUntilUtc { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed record CatalogSignerTrustChangeRecord
{
    public string ChangeId { get; init; } = string.Empty;
    public string Operation { get; init; } = string.Empty;
    public string Status { get; init; } = CatalogSignerTrustChangeStatus.Proposed;
    public string SignerId { get; init; } = string.Empty;
    public string KeyId { get; init; } = string.Empty;
    public string PublicKeyPem { get; init; } = string.Empty;
    public string PublicKeyFingerprint { get; init; } = string.Empty;
    public string RotatesFromKeyId { get; init; } = string.Empty;
    public DateTime? ValidFromUtc { get; init; }
    public DateTime? ValidUntilUtc { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string PolicyVersion { get; init; } = string.Empty;
    public string RequestedByUserId { get; init; } = string.Empty;
    public string ApprovedByUserId { get; init; } = string.Empty;
    public string AppliedByUserId { get; init; } = string.Empty;
    public string RejectedByUserId { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime? ApprovedAtUtc { get; init; }
    public DateTime? AppliedAtUtc { get; init; }
    public DateTime? RejectedAtUtc { get; init; }
    public string ResultingTrustStoreVersion { get; init; } = string.Empty;
}

public sealed class CatalogSignerTrustMutationResult
{
    public bool IsAccepted { get; init; }
    public CatalogSignerTrustChangeRecord? Change { get; init; }
    public IReadOnlyList<string> Blockers { get; init; } = Array.Empty<string>();
}

public sealed record CatalogSignerTrustStoreRecoveryResult
{
    public bool IsVerified { get; init; }
    public string Operation { get; init; } = string.Empty;
    public string SourcePath { get; init; } = string.Empty;
    public string BackupPath { get; init; } = string.Empty;
    public string TrustStoreHash { get; init; } = string.Empty;
    public string ExpectedTrustStoreHash { get; init; } = string.Empty;
    public string TrustStoreVersion { get; init; } = string.Empty;
    public int SignerCount { get; init; }
    public int ChangeCount { get; init; }
    public DateTime VerifiedAtUtc { get; init; } = DateTime.UtcNow;
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}

public sealed record CatalogCertificationEvidenceRecord
{
    public string EvidenceId { get; init; } = string.Empty;
    public string EnvironmentName { get; init; } = string.Empty;
    public string EvidenceKind { get; init; } = string.Empty;
    public string Status { get; init; } = CatalogCertificationEvidenceStatus.Recorded;
    public string Summary { get; init; } = string.Empty;
    public string EvidenceUri { get; init; } = string.Empty;
    public string RelatedChangeId { get; init; } = string.Empty;
    public string RelatedIncidentId { get; init; } = string.Empty;
    public string OperatorUserId { get; init; } = string.Empty;
    public string ApprovedByUserId { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
    public DateTime CapturedAtUtc { get; init; } = DateTime.UtcNow;
    public string ArtifactHash { get; init; } = string.Empty;
    public string ArtifactHashAlgorithm { get; init; } = "sha256";
    public string ArtifactContentType { get; init; } = string.Empty;
    public string ArtifactType { get; init; } = string.Empty;
    public string SourceSystem { get; init; } = string.Empty;
    public DateTime? CollectedAtUtc { get; init; }
    public string VerificationStatus { get; init; } = CatalogEvidenceVerificationStatus.Unverified;
    public string VerificationNotes { get; init; } = string.Empty;
    public string VerifiedByUserId { get; init; } = string.Empty;
    public DateTime? VerifiedAtUtc { get; init; }
    public DateTime? ExpiresAtUtc { get; init; }
    public string SupersededByEvidenceId { get; init; } = string.Empty;
    public string TrustTier { get; init; } = string.Empty;
    public string ArtifactProvider { get; init; } = string.Empty;
    public DateTime? ProviderVerifiedAtUtc { get; init; }
    public long? ArtifactSizeBytes { get; init; }
    public string SignedPayload { get; init; } = string.Empty;
    public string Signature { get; init; } = string.Empty;
    public string SignatureAlgorithm { get; init; } = string.Empty;
    public string SignerId { get; init; } = string.Empty;
    public string SignerPublicKeyId { get; init; } = string.Empty;
    public DateTime? SignatureVerifiedAtUtc { get; init; }
    public string VerificationMethod { get; init; } = string.Empty;
    public string VerificationPolicyVersion { get; init; } = string.Empty;
    public string SignerPublicKeyFingerprint { get; init; } = string.Empty;
    public string SignerStatusAtVerification { get; init; } = string.Empty;
    public string SignerTrustStoreVersion { get; init; } = string.Empty;
    public DateTime? SignerValidFromUtc { get; init; }
    public DateTime? SignerValidUntilUtc { get; init; }
}

public sealed class CatalogEvidenceVerificationResult
{
    public bool IsVerified { get; init; }
    public string EvidenceId { get; init; } = string.Empty;
    public string Status { get; init; } = CatalogCertificationEvidenceStatus.Recorded;
    public string VerificationStatus { get; init; } = CatalogEvidenceVerificationStatus.Failed;
    public string VerificationNotes { get; init; } = string.Empty;
    public string VerifiedByUserId { get; init; } = string.Empty;
    public DateTime VerifiedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime? ExpiresAtUtc { get; init; }
    public string TrustTier { get; init; } = string.Empty;
    public string ArtifactProvider { get; init; } = string.Empty;
    public DateTime? ProviderVerifiedAtUtc { get; init; }
    public long? ArtifactSizeBytes { get; init; }
    public string SignerId { get; init; } = string.Empty;
    public string SignerPublicKeyId { get; init; } = string.Empty;
    public DateTime? SignatureVerifiedAtUtc { get; init; }
    public string VerificationMethod { get; init; } = string.Empty;
    public string VerificationPolicyVersion { get; init; } = string.Empty;
    public string SignerPublicKeyFingerprint { get; init; } = string.Empty;
    public string SignerStatusAtVerification { get; init; } = string.Empty;
    public string SignerTrustStoreVersion { get; init; } = string.Empty;
    public DateTime? SignerValidFromUtc { get; init; }
    public DateTime? SignerValidUntilUtc { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}

public sealed class CatalogEvidenceSignatureVerificationResult
{
    public bool WasSignaturePresent { get; init; }
    public bool IsVerified { get; init; }
    public string SignerId { get; init; } = string.Empty;
    public string SignerPublicKeyId { get; init; } = string.Empty;
    public string SignatureAlgorithm { get; init; } = string.Empty;
    public DateTime? SignatureVerifiedAtUtc { get; init; }
    public string SignerPublicKeyFingerprint { get; init; } = string.Empty;
    public string SignerStatusAtVerification { get; init; } = string.Empty;
    public string SignerTrustStoreVersion { get; init; } = string.Empty;
    public DateTime? SignerValidFromUtc { get; init; }
    public DateTime? SignerValidUntilUtc { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}

public sealed class CatalogEvidenceTrustEvaluation
{
    public bool IsTrusted { get; init; }
    public string EvidenceId { get; init; } = string.Empty;
    public string TrustTier { get; init; } = string.Empty;
    public string RequiredTrustTier { get; init; } = string.Empty;
    public bool IsFresh { get; init; }
    public DateTime? FreshUntilUtc { get; init; }
    public string VerificationMethod { get; init; } = string.Empty;
    public string VerificationPolicyVersion { get; init; } = string.Empty;
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Failures { get; init; } = Array.Empty<string>();
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
    public IReadOnlyList<string> EvidenceUntrusted { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> EvidenceTrustFailures { get; init; } = Array.Empty<string>();
    public CatalogMutationPreviewResult? Preview { get; init; }
}

public sealed class CatalogControlPlaneSloDefinition
{
    public string Name { get; init; } = string.Empty;
    public string Target { get; init; } = string.Empty;
    public string AlertCondition { get; init; } = string.Empty;
    public string Escalation { get; init; } = string.Empty;
}
