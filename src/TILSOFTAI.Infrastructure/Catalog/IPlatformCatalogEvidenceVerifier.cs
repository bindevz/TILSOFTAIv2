namespace TILSOFTAI.Infrastructure.Catalog;

public interface IPlatformCatalogEvidenceVerifier
{
    CatalogEvidenceVerificationResult Verify(
        CatalogCertificationEvidenceRecord evidence,
        CatalogMutationContext context,
        bool acceptAsTrusted,
        string verificationNotes);

    bool IsTrusted(CatalogCertificationEvidenceRecord evidence, DateTime utcNow);

    CatalogEvidenceTrustEvaluation EvaluateTrust(CatalogCertificationEvidenceRecord evidence, DateTime utcNow);
}
