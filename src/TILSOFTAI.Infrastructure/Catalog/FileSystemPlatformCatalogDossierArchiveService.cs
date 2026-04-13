using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Infrastructure.Catalog;

public sealed partial class FileSystemPlatformCatalogDossierArchiveService : IPlatformCatalogDossierArchiveService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly CatalogCertificationOptions _options;

    public FileSystemPlatformCatalogDossierArchiveService(IOptions<CatalogCertificationOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<CatalogDossierArchiveRecord?> GetArchiveAsync(string manifestId, CancellationToken ct)
    {
        var path = ArchivePath(manifestId);
        if (!File.Exists(path))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(path, ct);
        var envelope = JsonSerializer.Deserialize<CatalogDossierArchiveEnvelope>(json, JsonOptions);
        return envelope?.Archive;
    }

    public async Task<CatalogDossierArchiveRecord> ArchiveAsync(
        CatalogPromotionDossier dossier,
        CatalogMutationContext context,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dossier);
        ArgumentNullException.ThrowIfNull(context);

        Directory.CreateDirectory(ArchiveRootPath());
        var now = DateTime.UtcNow;
        var path = ArchivePath(dossier.Manifest.ManifestId);
        var archive = new CatalogDossierArchiveRecord
        {
            ManifestId = dossier.Manifest.ManifestId,
            DossierHash = dossier.DossierHash,
            ArchivePath = path,
            PolicyVersion = _options.PolicyVersion,
            CreatedByUserId = context.UserId,
            CreatedAtUtc = now,
            RetainUntilUtc = dossier.Retention.DossierArchiveRetainUntilUtc
        };

        var sealPayload = new
        {
            archive.ManifestId,
            archive.DossierHash,
            archive.PolicyVersion,
            archive.CreatedByUserId,
            CreatedAtUtc = archive.CreatedAtUtc.ToUniversalTime().ToString("O"),
            archive.RetainUntilUtc,
            dossier.Manifest.ManifestHash,
            Evidence = dossier.Evidence.Select(item => new
            {
                item.EvidenceId,
                item.ArtifactHash,
                item.TrustTier,
                item.SignatureVerifiedAtUtc,
                item.VerificationPolicyVersion
            }).OrderBy(item => item.EvidenceId, StringComparer.Ordinal).ToArray()
        };
        archive = archive with { ArchiveHash = Sha256(JsonSerializer.Serialize(sealPayload, JsonOptions)) };

        var envelope = new CatalogDossierArchiveEnvelope
        {
            Archive = archive,
            Dossier = dossier with { Archive = archive }
        };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(envelope, JsonOptions), ct);
        return archive;
    }

    private string ArchivePath(string manifestId) =>
        Path.Combine(ArchiveRootPath(), $"{SafeFileName(manifestId)}.dossier.archive.json");

    private string ArchiveRootPath() => Path.GetFullPath(_options.DossierArchiveRootPath);

    private static string SafeFileName(string value)
    {
        var cleaned = SafeFileNameRegex().Replace(value.Trim(), "_");
        return string.IsNullOrWhiteSpace(cleaned) ? "manifest" : cleaned;
    }

    private static string Sha256(string payload) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

    [GeneratedRegex("[^a-zA-Z0-9_.-]")]
    private static partial Regex SafeFileNameRegex();

    private sealed class CatalogDossierArchiveEnvelope
    {
        public CatalogDossierArchiveRecord? Archive { get; init; }
        public CatalogPromotionDossier? Dossier { get; init; }
    }
}
