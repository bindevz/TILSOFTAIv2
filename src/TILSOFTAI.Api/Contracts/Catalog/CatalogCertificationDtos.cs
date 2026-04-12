using TILSOFTAI.Infrastructure.Catalog;

namespace TILSOFTAI.Api.Contracts.Catalog;

public sealed class CatalogCertificationEvidenceApiRequest
{
    public string EnvironmentName { get; init; } = string.Empty;
    public string EvidenceKind { get; init; } = string.Empty;
    public string Status { get; init; } = CatalogCertificationEvidenceStatus.Pending;
    public string Summary { get; init; } = string.Empty;
    public string EvidenceUri { get; init; } = string.Empty;
    public string RelatedChangeId { get; init; } = string.Empty;
    public string RelatedIncidentId { get; init; } = string.Empty;
    public string ApprovedByUserId { get; init; } = string.Empty;
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
