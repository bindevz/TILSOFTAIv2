namespace TILSOFTAI.Infrastructure.Catalog;

public interface IPlatformCatalogPromotionManifestService
{
    Task<CatalogPromotionManifestIssueResult> IssueManifestAsync(
        CatalogPromotionManifestIssueRequest request,
        CatalogMutationContext context,
        CancellationToken ct);

    Task<CatalogRolloutAttestationResult> RecordAttestationAsync(
        string manifestId,
        CatalogRolloutAttestationRequest request,
        CatalogMutationContext context,
        CancellationToken ct);

    Task<CatalogPromotionDossier?> GetDossierAsync(
        string manifestId,
        CatalogMutationContext context,
        CancellationToken ct);

    Task<CatalogDossierArchiveResult> ArchiveDossierAsync(
        string manifestId,
        CatalogMutationContext context,
        CancellationToken ct);
}
