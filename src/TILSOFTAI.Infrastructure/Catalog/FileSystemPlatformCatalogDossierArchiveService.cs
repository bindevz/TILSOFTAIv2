using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
    private readonly IPlatformCatalogArchiveStorage _archiveStorage;

    public FileSystemPlatformCatalogDossierArchiveService(IOptions<CatalogCertificationOptions> options)
        : this(options, new FileSystemPlatformCatalogArchiveStorage(options))
    {
    }

    public FileSystemPlatformCatalogDossierArchiveService(
        IOptions<CatalogCertificationOptions> options,
        IPlatformCatalogArchiveStorage archiveStorage)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _archiveStorage = archiveStorage ?? throw new ArgumentNullException(nameof(archiveStorage));
    }

    public async Task<CatalogDossierArchiveRecord?> GetArchiveAsync(string manifestId, CancellationToken ct)
    {
        var stored = await _archiveStorage.ReadAsync(manifestId, ct);
        if (!stored.Found)
        {
            return null;
        }

        var envelope = JsonSerializer.Deserialize<CatalogDossierArchiveEnvelope>(stored.Content, JsonOptions);
        return envelope?.Archive is null
            ? null
            : WithStorageMetadata(envelope.Archive, stored);
    }

    public async Task<CatalogDossierArchiveRecord> ArchiveAsync(
        CatalogPromotionDossier dossier,
        CatalogMutationContext context,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dossier);
        ArgumentNullException.ThrowIfNull(context);

        var now = DateTime.UtcNow;
        var archive = new CatalogDossierArchiveRecord
        {
            ManifestId = dossier.Manifest.ManifestId,
            DossierHash = dossier.DossierHash,
            PolicyVersion = _options.PolicyVersion,
            CreatedByUserId = context.UserId,
            CreatedAtUtc = now,
            RetainUntilUtc = dossier.Retention.DossierArchiveRetainUntilUtc
        };

        archive = archive with { ArchiveHash = ComputeArchiveHash(archive, dossier) };

        var envelope = new CatalogDossierArchiveEnvelope
        {
            Archive = archive,
            Dossier = dossier with { Archive = archive }
        };
        var stored = await _archiveStorage.WriteAsync(dossier.Manifest.ManifestId, JsonSerializer.Serialize(envelope, JsonOptions), ct);
        return WithStorageMetadata(archive, stored);
    }

    public async Task<CatalogDossierArchiveVerificationResult> VerifyArchiveAsync(
        string manifestId,
        CancellationToken ct)
    {
        var stored = await _archiveStorage.ReadAsync(manifestId, ct);
        if (!stored.Found)
        {
            return new CatalogDossierArchiveVerificationResult
            {
                IsVerified = false,
                ManifestId = manifestId,
                BackendName = stored.BackendName,
                BackendClass = stored.BackendClass,
                RetentionPosture = stored.RetentionPosture,
                ImmutabilityEnforced = stored.ImmutabilityEnforced,
                StorageUri = stored.StorageUri,
                RecoveryState = stored.RecoveryState,
                Errors = new[] { "archive_not_found" }
            };
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<CatalogDossierArchiveEnvelope>(stored.Content, JsonOptions);
            if (envelope?.Archive is null || envelope.Dossier is null)
            {
                return VerificationFailed(manifestId, stored, "archive_envelope_invalid");
            }

            var computed = ComputeArchiveHash(envelope.Archive, envelope.Dossier);
            var errors = new List<string>();
            if (!string.Equals(envelope.Archive.ArchiveHash, computed, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("archive_hash_mismatch");
            }

            if (!string.Equals(envelope.Archive.ManifestId, envelope.Dossier.Manifest.ManifestId, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("archive_manifest_mismatch");
            }

            return new CatalogDossierArchiveVerificationResult
            {
                IsVerified = errors.Count == 0,
                ManifestId = envelope.Archive.ManifestId,
                DossierHash = envelope.Archive.DossierHash,
                ArchiveHash = envelope.Archive.ArchiveHash,
                ComputedArchiveHash = computed,
                BackendName = stored.BackendName,
                BackendClass = stored.BackendClass,
                RetentionPosture = stored.RetentionPosture,
                ImmutabilityEnforced = stored.ImmutabilityEnforced,
                StorageUri = stored.StorageUri,
                RecoveryState = stored.RecoveryState,
                PolicyVersion = envelope.Archive.PolicyVersion,
                VerifiedAtUtc = DateTime.UtcNow,
                Errors = errors
            };
        }
        catch (JsonException)
        {
            return VerificationFailed(manifestId, stored, "archive_json_invalid");
        }
    }

    private static CatalogDossierArchiveVerificationResult VerificationFailed(
        string manifestId,
        CatalogArchiveStorageReadResult stored,
        string error) => new()
        {
            IsVerified = false,
            ManifestId = manifestId,
            BackendName = stored.BackendName,
            BackendClass = stored.BackendClass,
            RetentionPosture = stored.RetentionPosture,
            ImmutabilityEnforced = stored.ImmutabilityEnforced,
            StorageUri = stored.StorageUri,
            RecoveryState = stored.RecoveryState,
            VerifiedAtUtc = DateTime.UtcNow,
            Errors = new[] { error }
        };

    private static CatalogDossierArchiveRecord WithStorageMetadata(
        CatalogDossierArchiveRecord archive,
        CatalogArchiveStorageWriteResult stored) =>
        archive with
        {
            BackendName = stored.BackendName,
            BackendClass = stored.BackendClass,
            RetentionPosture = stored.RetentionPosture,
            ImmutabilityEnforced = stored.ImmutabilityEnforced,
            ArchivePath = stored.ArchivePath,
            StorageUri = stored.StorageUri,
            RecoveryState = stored.RecoveryState
        };

    private static CatalogDossierArchiveRecord WithStorageMetadata(
        CatalogDossierArchiveRecord archive,
        CatalogArchiveStorageReadResult stored) =>
        archive with
        {
            BackendName = stored.BackendName,
            BackendClass = stored.BackendClass,
            RetentionPosture = stored.RetentionPosture,
            ImmutabilityEnforced = stored.ImmutabilityEnforced,
            ArchivePath = stored.ArchivePath,
            StorageUri = stored.StorageUri,
            RecoveryState = stored.RecoveryState
        };

    private static string ComputeArchiveHash(CatalogDossierArchiveRecord archive, CatalogPromotionDossier dossier)
    {
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
                item.VerificationPolicyVersion,
                item.SignerId,
                item.SignerPublicKeyId,
                item.SignerPublicKeyFingerprint,
                item.SignerTrustStoreVersion
            }).OrderBy(item => item.EvidenceId, StringComparer.Ordinal).ToArray()
        };
        return Sha256(JsonSerializer.Serialize(sealPayload, JsonOptions));
    }

    private static string Sha256(string payload) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

    private sealed class CatalogDossierArchiveEnvelope
    {
        public CatalogDossierArchiveRecord? Archive { get; init; }
        public CatalogPromotionDossier? Dossier { get; init; }
    }
}
