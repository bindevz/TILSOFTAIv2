namespace TILSOFTAI.Infrastructure.Catalog;

public interface IPlatformCatalogArtifactProvider
{
    CatalogArtifactVerificationResult Verify(CatalogCertificationEvidenceRecord evidence);
}

public sealed class CatalogArtifactVerificationResult
{
    public bool WasProviderControlled { get; init; }
    public bool IsVerified { get; init; }
    public string ProviderName { get; init; } = string.Empty;
    public string ComputedSha256 { get; init; } = string.Empty;
    public long? ArtifactSizeBytes { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}
