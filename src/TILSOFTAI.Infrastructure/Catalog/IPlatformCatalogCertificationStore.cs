namespace TILSOFTAI.Infrastructure.Catalog;

public interface IPlatformCatalogCertificationStore
{
    Task<IReadOnlyList<CatalogCertificationEvidenceRecord>> ListEvidenceAsync(
        string environmentName,
        CancellationToken ct);

    Task<CatalogCertificationEvidenceRecord?> GetEvidenceAsync(
        string evidenceId,
        CancellationToken ct);

    Task<CatalogCertificationEvidenceRecord> CreateEvidenceAsync(
        CatalogCertificationEvidenceRecord evidence,
        CancellationToken ct);

    Task<CatalogCertificationEvidenceRecord> UpdateEvidenceVerificationAsync(
        string evidenceId,
        CatalogEvidenceVerificationResult result,
        CancellationToken ct);
}
