using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Infrastructure.Catalog;

public sealed class FileSystemPlatformCatalogTrustStoreRecoveryStorage : IPlatformCatalogTrustStoreRecoveryStorage
{
    private readonly CatalogCertificationOptions _options;

    public FileSystemPlatformCatalogTrustStoreRecoveryStorage(IOptions<CatalogCertificationOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public string BackupBackendName => "filesystem";
    public string BackupBackendClass => "local_filesystem";
    public string CustodyBoundary => "same_host_family";

    public CatalogTrustStoreRecoveryStorageWriteResult Write(string content)
    {
        var backup = BackupPath();
        var directory = Path.GetDirectoryName(backup);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(backup, content);
        return Result(backup);
    }

    public CatalogTrustStoreRecoveryStorageReadResult Read()
    {
        var backup = BackupPath();
        if (!File.Exists(backup))
        {
            return new CatalogTrustStoreRecoveryStorageReadResult
            {
                Found = false,
                BackupPath = backup,
                BackupBackendName = BackupBackendName,
                BackupBackendClass = BackupBackendClass,
                CustodyBoundary = CustodyBoundary
            };
        }

        return new CatalogTrustStoreRecoveryStorageReadResult
        {
            Found = true,
            BackupPath = backup,
            BackupBackendName = BackupBackendName,
            BackupBackendClass = BackupBackendClass,
            CustodyBoundary = CustodyBoundary,
            Content = File.ReadAllText(backup)
        };
    }

    private string BackupPath() => Path.GetFullPath(_options.SignerTrustStoreBackupPath);

    private CatalogTrustStoreRecoveryStorageWriteResult Result(string backup) => new()
    {
        BackupPath = backup,
        BackupBackendName = BackupBackendName,
        BackupBackendClass = BackupBackendClass,
        CustodyBoundary = CustodyBoundary
    };
}
