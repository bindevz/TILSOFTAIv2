using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Infrastructure.Catalog;

public sealed class RsaPlatformCatalogSignatureVerifier : IPlatformCatalogSignatureVerifier
{
    private readonly CatalogCertificationOptions _options;
    private readonly IPlatformCatalogSignerTrustStore _trustStore;

    public RsaPlatformCatalogSignatureVerifier(IOptions<CatalogCertificationOptions> options)
        : this(options, new FileSystemPlatformCatalogSignerTrustStore(options))
    {
    }

    public RsaPlatformCatalogSignatureVerifier(
        IOptions<CatalogCertificationOptions> options,
        IPlatformCatalogSignerTrustStore trustStore)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _trustStore = trustStore ?? throw new ArgumentNullException(nameof(trustStore));
    }

    public CatalogEvidenceSignatureVerificationResult Verify(CatalogCertificationEvidenceRecord evidence, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        var hasAnySignatureField = !string.IsNullOrWhiteSpace(evidence.SignedPayload)
            || !string.IsNullOrWhiteSpace(evidence.Signature)
            || !string.IsNullOrWhiteSpace(evidence.SignerId);
        if (!hasAnySignatureField)
        {
            return new CatalogEvidenceSignatureVerificationResult();
        }

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(evidence.SignedPayload))
        {
            errors.Add("signature_payload_required");
        }

        if (string.IsNullOrWhiteSpace(evidence.Signature))
        {
            errors.Add("signature_required");
        }

        if (string.IsNullOrWhiteSpace(evidence.SignerId))
        {
            errors.Add("signature_signer_required");
        }

        var algorithm = string.IsNullOrWhiteSpace(evidence.SignatureAlgorithm)
            ? "RS256"
            : evidence.SignatureAlgorithm.Trim();
        if (_options.AllowedSignatureAlgorithms.Length > 0
            && !_options.AllowedSignatureAlgorithms.Any(item => string.Equals(item, algorithm, StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add("signature_algorithm_not_allowed");
        }

        var signer = _trustStore.FindSigner(evidence.SignerId, evidence.SignerPublicKeyId);
        if (signer is null)
        {
            errors.Add("signature_signer_not_trusted");
        }
        else
        {
            errors.AddRange(ValidateSignerLifecycle(signer, utcNow));
        }

        if (errors.Count > 0)
        {
            return Failed(evidence, signer, algorithm, errors);
        }

        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(signer!.PublicKeyPem);
            var signatureBytes = Convert.FromBase64String(evidence.Signature.Trim());
            var verified = rsa.VerifyData(
                Encoding.UTF8.GetBytes(evidence.SignedPayload),
                signatureBytes,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            if (!verified)
            {
                return Failed(evidence, signer, algorithm, new[] { "signature_invalid" });
            }

            return new CatalogEvidenceSignatureVerificationResult
            {
                WasSignaturePresent = true,
                IsVerified = true,
                SignerId = signer.SignerId,
                SignerPublicKeyId = signer.KeyId,
                SignatureAlgorithm = algorithm,
                SignatureVerifiedAtUtc = utcNow,
                SignerPublicKeyFingerprint = signer.PublicKeyFingerprint,
                SignerStatusAtVerification = signer.Status,
                SignerTrustStoreVersion = signer.TrustStoreVersion,
                SignerValidFromUtc = signer.ValidFromUtc,
                SignerValidUntilUtc = signer.ValidUntilUtc
            };
        }
        catch (FormatException)
        {
            return Failed(evidence, signer, algorithm, new[] { "signature_base64_invalid" });
        }
        catch (CryptographicException)
        {
            return Failed(evidence, signer, algorithm, new[] { "signature_key_invalid" });
        }
    }

    private static IReadOnlyList<string> ValidateSignerLifecycle(CatalogTrustedSignerRecord signer, DateTime utcNow)
    {
        var errors = new List<string>();
        if (!string.Equals(signer.Status, CatalogSignerLifecycleStates.Active, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"signature_signer_not_active:{signer.Status}");
        }

        if (signer.ValidFromUtc.HasValue && signer.ValidFromUtc.Value > utcNow)
        {
            errors.Add("signature_signer_not_yet_valid");
        }

        if (signer.ValidUntilUtc.HasValue && signer.ValidUntilUtc.Value <= utcNow)
        {
            errors.Add("signature_signer_expired");
        }

        if (signer.RevokedAtUtc.HasValue && signer.RevokedAtUtc.Value <= utcNow)
        {
            errors.Add("signature_signer_revoked");
        }

        return errors;
    }

    private static CatalogEvidenceSignatureVerificationResult Failed(
        CatalogCertificationEvidenceRecord evidence,
        CatalogTrustedSignerRecord? signer,
        string algorithm,
        IReadOnlyList<string> errors) => new()
        {
            WasSignaturePresent = true,
            IsVerified = false,
            SignerId = evidence.SignerId,
            SignerPublicKeyId = evidence.SignerPublicKeyId,
            SignatureAlgorithm = algorithm,
            SignerPublicKeyFingerprint = signer?.PublicKeyFingerprint ?? string.Empty,
            SignerStatusAtVerification = signer?.Status ?? string.Empty,
            SignerTrustStoreVersion = signer?.TrustStoreVersion ?? string.Empty,
            SignerValidFromUtc = signer?.ValidFromUtc,
            SignerValidUntilUtc = signer?.ValidUntilUtc,
            Errors = errors
        };
}
