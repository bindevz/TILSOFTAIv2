using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TILSOFTAI.Infrastructure.Catalog;

public static class PlatformCatalogPromotionManifestHasher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string ComputeHash(CatalogPromotionManifestRecord manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var payload = new
        {
            manifest.ManifestId,
            manifest.EnvironmentName,
            manifest.Status,
            ChangeIds = manifest.ChangeIds.OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            EvidenceIds = manifest.EvidenceIds.OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            manifest.GateSummaryJson,
            manifest.RollbackOfManifestId,
            manifest.RelatedIncidentId,
            manifest.CreatedByUserId,
            manifest.IssuedByUserId,
            CreatedAtUtc = manifest.CreatedAtUtc.ToUniversalTime().ToString("O"),
            IssuedAtUtc = manifest.IssuedAtUtc.ToUniversalTime().ToString("O")
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string ComputeDossierHash(CatalogPromotionDossier dossier)
    {
        ArgumentNullException.ThrowIfNull(dossier);

        var payload = new
        {
            dossier.Manifest.ManifestId,
            dossier.Manifest.ManifestHash,
            ChangeIds = dossier.Changes.Select(item => item.ChangeId).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            Evidence = dossier.Evidence.Select(item => new
            {
                item.EvidenceId,
                item.ArtifactHash,
                item.TrustTier,
                item.VerificationStatus,
                item.VerificationMethod,
                item.VerificationPolicyVersion,
                item.SignerId,
                item.SignerPublicKeyId,
                item.SignatureVerifiedAtUtc,
                item.ExpiresAtUtc
            }).OrderBy(item => item.EvidenceId, StringComparer.Ordinal).ToArray(),
            Attestations = dossier.Attestations.Select(item => new
            {
                item.AttestationId,
                item.State,
                item.CreatedAtUtc
            }).OrderBy(item => item.AttestationId, StringComparer.Ordinal).ToArray(),
            dossier.Retention.PolicyVersion,
            dossier.Retention.ManifestRetainUntilUtc,
            dossier.Retention.EvidenceRetainUntilUtc,
            dossier.Retention.AttestationRetainUntilUtc,
            dossier.Retention.DossierArchiveRetainUntilUtc,
            dossier.Retention.ArchiveRequired
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
