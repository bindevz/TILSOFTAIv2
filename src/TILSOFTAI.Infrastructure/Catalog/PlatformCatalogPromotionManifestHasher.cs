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
}
