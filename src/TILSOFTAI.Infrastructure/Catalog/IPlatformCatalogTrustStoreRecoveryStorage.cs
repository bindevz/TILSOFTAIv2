namespace TILSOFTAI.Infrastructure.Catalog;

public sealed record CatalogTrustStoreRecoveryStorageWriteResult
{
    public string BackupPath { get; init; } = string.Empty;
    public string BackupBackendName { get; init; } = string.Empty;
    public string BackupBackendClass { get; init; } = string.Empty;
    public string CustodyBoundary { get; init; } = string.Empty;
}

public sealed record CatalogTrustStoreRecoveryStorageReadResult
{
    public bool Found { get; init; }
    public string BackupPath { get; init; } = string.Empty;
    public string BackupBackendName { get; init; } = string.Empty;
    public string BackupBackendClass { get; init; } = string.Empty;
    public string CustodyBoundary { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
}

public interface IPlatformCatalogTrustStoreRecoveryStorage
{
    string BackupBackendName { get; }
    string BackupBackendClass { get; }
    string CustodyBoundary { get; }

    CatalogTrustStoreRecoveryStorageWriteResult Write(string content);

    CatalogTrustStoreRecoveryStorageReadResult Read();
}
