namespace TILSOFTAI.Infrastructure.Catalog;

public interface IPlatformCatalogDossierArchiveService
{
    Task<CatalogDossierArchiveRecord?> GetArchiveAsync(string manifestId, CancellationToken ct);

    Task<CatalogDossierArchiveRecord> ArchiveAsync(
        CatalogPromotionDossier dossier,
        CatalogMutationContext context,
        CancellationToken ct);

    Task<CatalogDossierArchiveVerificationResult> VerifyArchiveAsync(
        string manifestId,
        CancellationToken ct);
}
