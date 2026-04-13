using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Infrastructure.Catalog;

public sealed partial class FileSystemPlatformCatalogArchiveStorage : IPlatformCatalogArchiveStorage
{
    private readonly CatalogCertificationOptions _options;

    public FileSystemPlatformCatalogArchiveStorage(IOptions<CatalogCertificationOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public string BackendName => "filesystem";

    public async Task<CatalogArchiveStorageWriteResult> WriteAsync(
        string manifestId,
        string content,
        CancellationToken ct)
    {
        var root = ArchiveRootPath();
        Directory.CreateDirectory(root);
        var path = ArchivePath(root, manifestId);
        await File.WriteAllTextAsync(path, content, ct);
        return new CatalogArchiveStorageWriteResult
        {
            BackendName = BackendName,
            ArchivePath = path,
            StorageUri = StorageUri(manifestId),
            RecoveryState = "primary_written"
        };
    }

    public async Task<CatalogArchiveStorageReadResult> ReadAsync(
        string manifestId,
        CancellationToken ct)
    {
        var root = ArchiveRootPath();
        var path = ArchivePath(root, manifestId);
        if (!File.Exists(path))
        {
            return new CatalogArchiveStorageReadResult
            {
                Found = false,
                BackendName = BackendName,
                ArchivePath = path,
                StorageUri = StorageUri(manifestId),
                RecoveryState = "primary_missing"
            };
        }

        return new CatalogArchiveStorageReadResult
        {
            Found = true,
            BackendName = BackendName,
            ArchivePath = path,
            StorageUri = StorageUri(manifestId),
            RecoveryState = "primary_read",
            Content = await File.ReadAllTextAsync(path, ct)
        };
    }

    private string ArchiveRootPath() => Path.GetFullPath(_options.DossierArchiveRootPath);

    private static string ArchivePath(string root, string manifestId) =>
        Path.Combine(root, $"{SafeFileName(manifestId)}.dossier.archive.json");

    private static string StorageUri(string manifestId) =>
        $"archive://filesystem/{SafeFileName(manifestId)}.dossier.archive.json";

    private static string SafeFileName(string value)
    {
        var cleaned = SafeFileNameRegex().Replace(value.Trim(), "_");
        return string.IsNullOrWhiteSpace(cleaned) ? "manifest" : cleaned;
    }

    [GeneratedRegex("[^a-zA-Z0-9_.-]")]
    private static partial Regex SafeFileNameRegex();
}
