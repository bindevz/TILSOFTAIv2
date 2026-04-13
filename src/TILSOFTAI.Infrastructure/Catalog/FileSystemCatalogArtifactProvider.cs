using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Infrastructure.Catalog;

public sealed class FileSystemCatalogArtifactProvider : IPlatformCatalogArtifactProvider
{
    private readonly CatalogCertificationOptions _options;

    public FileSystemCatalogArtifactProvider(IOptions<CatalogCertificationOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public CatalogArtifactVerificationResult Verify(CatalogCertificationEvidenceRecord evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        var prefix = _options.ControlledArtifactUriPrefixes
            .FirstOrDefault(item => evidence.EvidenceUri.StartsWith(item, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return new CatalogArtifactVerificationResult();
        }

        var root = Path.GetFullPath(_options.TrustedArtifactRootPath);
        var relative = evidence.EvidenceUri[prefix.Length..]
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        var path = Path.GetFullPath(Path.Combine(root, relative));
        if (!path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(path, root, StringComparison.OrdinalIgnoreCase))
        {
            return ProviderFailure("artifact_provider_path_escape");
        }

        if (!File.Exists(path))
        {
            return ProviderFailure("artifact_provider_file_missing");
        }

        var bytes = File.ReadAllBytes(path);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (!string.Equals(hash, evidence.ArtifactHash, StringComparison.OrdinalIgnoreCase))
        {
            return new CatalogArtifactVerificationResult
            {
                WasProviderControlled = true,
                IsVerified = false,
                ProviderName = "filesystem",
                ComputedSha256 = hash,
                ArtifactSizeBytes = bytes.LongLength,
                Errors = new[] { "artifact_provider_hash_mismatch" }
            };
        }

        return new CatalogArtifactVerificationResult
        {
            WasProviderControlled = true,
            IsVerified = true,
            ProviderName = "filesystem",
            ComputedSha256 = hash,
            ArtifactSizeBytes = bytes.LongLength
        };
    }

    private static CatalogArtifactVerificationResult ProviderFailure(string error) => new()
    {
        WasProviderControlled = true,
        IsVerified = false,
        ProviderName = "filesystem",
        Errors = new[] { error }
    };
}
