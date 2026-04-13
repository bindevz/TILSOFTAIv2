using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Infrastructure.Catalog;

public sealed partial class MirroredPlatformCatalogArchiveStorage : IPlatformCatalogArchiveStorage
{
    private readonly CatalogCertificationOptions _options;
    private readonly FileSystemPlatformCatalogArchiveStorage _primary;

    public MirroredPlatformCatalogArchiveStorage(IOptions<CatalogCertificationOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _primary = new FileSystemPlatformCatalogArchiveStorage(options);
    }

    public string BackendName => _options.EnableDossierArchiveMirror ? "filesystem+mirror" : "filesystem";

    public async Task<CatalogArchiveStorageWriteResult> WriteAsync(
        string manifestId,
        string content,
        CancellationToken ct)
    {
        var primary = await _primary.WriteAsync(manifestId, content, ct);
        if (!_options.EnableDossierArchiveMirror)
        {
            return primary with { BackendName = BackendName, RecoveryState = "primary_written" };
        }

        var mirrorPath = MirrorPath(manifestId);
        Directory.CreateDirectory(Path.GetDirectoryName(mirrorPath)!);
        await File.WriteAllTextAsync(mirrorPath, content, ct);
        return primary with
        {
            BackendName = BackendName,
            StorageUri = $"{primary.StorageUri};mirror={MirrorStorageUri(manifestId)}",
            RecoveryState = "primary_and_mirror_written"
        };
    }

    public async Task<CatalogArchiveStorageReadResult> ReadAsync(
        string manifestId,
        CancellationToken ct)
    {
        var primary = await _primary.ReadAsync(manifestId, ct);
        if (primary.Found)
        {
            return primary with
            {
                BackendName = BackendName,
                RecoveryState = _options.EnableDossierArchiveMirror ? "primary_read_mirror_available" : "primary_read"
            };
        }

        if (!_options.EnableDossierArchiveMirror)
        {
            return primary with { BackendName = BackendName, RecoveryState = "primary_missing" };
        }

        var mirrorPath = MirrorPath(manifestId);
        if (!File.Exists(mirrorPath))
        {
            return primary with
            {
                BackendName = BackendName,
                StorageUri = $"{primary.StorageUri};mirror={MirrorStorageUri(manifestId)}",
                RecoveryState = "primary_and_mirror_missing"
            };
        }

        return new CatalogArchiveStorageReadResult
        {
            Found = true,
            BackendName = BackendName,
            ArchivePath = mirrorPath,
            StorageUri = MirrorStorageUri(manifestId),
            RecoveryState = "recovered_from_mirror",
            Content = await File.ReadAllTextAsync(mirrorPath, ct)
        };
    }

    private string MirrorPath(string manifestId) =>
        Path.Combine(Path.GetFullPath(_options.DossierArchiveMirrorRootPath), $"{SafeFileName(manifestId)}.dossier.archive.json");

    private static string MirrorStorageUri(string manifestId) =>
        $"archive://filesystem-mirror/{SafeFileName(manifestId)}.dossier.archive.json";

    private static string SafeFileName(string value)
    {
        var cleaned = SafeFileNameRegex().Replace(value.Trim(), "_");
        return string.IsNullOrWhiteSpace(cleaned) ? "manifest" : cleaned;
    }

    [GeneratedRegex("[^a-zA-Z0-9_.-]")]
    private static partial Regex SafeFileNameRegex();
}
