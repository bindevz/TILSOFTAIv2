namespace TILSOFTAI.Infrastructure.Catalog;

public interface IPlatformCatalogPromotionManifestStore
{
    Task<CatalogPromotionManifestRecord> CreateManifestAsync(
        CatalogPromotionManifestRecord manifest,
        CancellationToken ct);

    Task<CatalogPromotionManifestRecord?> GetManifestAsync(
        string manifestId,
        CancellationToken ct);

    Task<IReadOnlyList<CatalogPromotionManifestRecord>> ListManifestsAsync(
        string environmentName,
        CancellationToken ct);

    Task<CatalogRolloutAttestationRecord> CreateAttestationAsync(
        CatalogRolloutAttestationRecord attestation,
        CancellationToken ct);

    Task<IReadOnlyList<CatalogRolloutAttestationRecord>> ListAttestationsAsync(
        string manifestId,
        CancellationToken ct);
}
