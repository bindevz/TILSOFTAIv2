namespace TILSOFTAI.Infrastructure.Catalog;

public interface IPlatformCatalogSignatureVerifier
{
    CatalogEvidenceSignatureVerificationResult Verify(CatalogCertificationEvidenceRecord evidence, DateTime utcNow);
}
