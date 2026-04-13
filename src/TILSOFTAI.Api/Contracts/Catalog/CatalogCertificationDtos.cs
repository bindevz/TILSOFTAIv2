using TILSOFTAI.Infrastructure.Catalog;

namespace TILSOFTAI.Api.Contracts.Catalog;

public sealed class CatalogCertificationEvidenceApiRequest
{
    public string EnvironmentName { get; init; } = string.Empty;
    public string EvidenceKind { get; init; } = string.Empty;
    public string Status { get; init; } = CatalogCertificationEvidenceStatus.Recorded;
    public string Summary { get; init; } = string.Empty;
    public string EvidenceUri { get; init; } = string.Empty;
    public string RelatedChangeId { get; init; } = string.Empty;
    public string RelatedIncidentId { get; init; } = string.Empty;
    public string ApprovedByUserId { get; init; } = string.Empty;
    public string ArtifactHash { get; init; } = string.Empty;
    public string ArtifactHashAlgorithm { get; init; } = "sha256";
    public string ArtifactContentType { get; init; } = string.Empty;
    public string ArtifactType { get; init; } = string.Empty;
    public string SourceSystem { get; init; } = string.Empty;
    public DateTime? CollectedAtUtc { get; init; }
    public string SignedPayload { get; init; } = string.Empty;
    public string Signature { get; init; } = string.Empty;
    public string SignatureAlgorithm { get; init; } = string.Empty;
    public string SignerId { get; init; } = string.Empty;
    public string SignerPublicKeyId { get; init; } = string.Empty;
}

public sealed class CatalogCertificationEvidenceVerifyApiRequest
{
    public bool AcceptAsTrusted { get; init; }
    public string VerificationNotes { get; init; } = string.Empty;
}

public sealed class CatalogPromotionGateApiRequest
{
    public string EnvironmentName { get; init; } = string.Empty;
    public string ChangeId { get; init; } = string.Empty;
    public CatalogMutationApiRequest? MutationPreview { get; init; }
    public bool IncludeCertificationEvidence { get; init; } = true;

    public CatalogPromotionGateRequest ToGateRequest() => new()
    {
        EnvironmentName = EnvironmentName,
        ChangeId = ChangeId,
        MutationPreview = MutationPreview?.ToMutationRequest(),
        IncludeCertificationEvidence = IncludeCertificationEvidence
    };
}

public sealed class CatalogPromotionManifestIssueApiRequest
{
    public string EnvironmentName { get; init; } = string.Empty;
    public string[] ChangeIds { get; init; } = Array.Empty<string>();
    public string[] EvidenceIds { get; init; } = Array.Empty<string>();
    public string RollbackOfManifestId { get; init; } = string.Empty;
    public string RelatedIncidentId { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;

    public CatalogPromotionManifestIssueRequest ToIssueRequest() => new()
    {
        EnvironmentName = EnvironmentName,
        ChangeIds = ChangeIds,
        EvidenceIds = EvidenceIds,
        RollbackOfManifestId = RollbackOfManifestId,
        RelatedIncidentId = RelatedIncidentId,
        Notes = Notes
    };
}

public sealed class CatalogRolloutAttestationApiRequest
{
    public string State { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
    public string[] EvidenceIds { get; init; } = Array.Empty<string>();
    public string AcceptedByUserId { get; init; } = string.Empty;

    public CatalogRolloutAttestationRequest ToAttestationRequest() => new()
    {
        State = State,
        Notes = Notes,
        EvidenceIds = EvidenceIds,
        AcceptedByUserId = AcceptedByUserId
    };
}

public sealed class CatalogSignerTrustMutationApiRequest
{
    public string Operation { get; init; } = string.Empty;
    public string SignerId { get; init; } = string.Empty;
    public string KeyId { get; init; } = string.Empty;
    public string PublicKeyPem { get; init; } = string.Empty;
    public string RotatesFromKeyId { get; init; } = string.Empty;
    public DateTime? ValidFromUtc { get; init; }
    public DateTime? ValidUntilUtc { get; init; }
    public string Reason { get; init; } = string.Empty;

    public CatalogSignerTrustMutationRequest ToMutationRequest() => new()
    {
        Operation = Operation,
        SignerId = SignerId,
        KeyId = KeyId,
        PublicKeyPem = PublicKeyPem,
        RotatesFromKeyId = RotatesFromKeyId,
        ValidFromUtc = ValidFromUtc,
        ValidUntilUtc = ValidUntilUtc,
        Reason = Reason
    };
}

public sealed class CatalogSignerTrustRejectApiRequest
{
    public string Reason { get; init; } = string.Empty;
}

public sealed class CatalogTrustStoreRecoveryApiRequest
{
    public string ExpectedTrustStoreHash { get; init; } = string.Empty;
}
