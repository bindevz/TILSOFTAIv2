namespace TILSOFTAI.Infrastructure.Catalog;

public interface IPlatformCatalogSignerTrustStore
{
    IReadOnlyList<CatalogTrustedSignerRecord> ListSigners();

    IReadOnlyList<CatalogSignerTrustChangeRecord> ListChanges();

    CatalogTrustedSignerRecord? FindSigner(string signerId, string keyId);

    CatalogSignerTrustMutationResult ProposeChange(
        CatalogSignerTrustMutationRequest request,
        CatalogMutationContext context);

    CatalogSignerTrustMutationResult ApproveChange(
        string changeId,
        CatalogMutationContext context);

    CatalogSignerTrustMutationResult RejectChange(
        string changeId,
        CatalogMutationContext context,
        string reason);

    CatalogSignerTrustMutationResult ApplyChange(
        string changeId,
        CatalogMutationContext context);

    CatalogSignerTrustStoreRecoveryResult BackupTrustStore();

    CatalogSignerTrustStoreRecoveryResult VerifyTrustStoreBackup(string expectedTrustStoreHash);

    CatalogSignerTrustStoreRecoveryResult RestoreTrustStoreBackup(string expectedTrustStoreHash);
}
