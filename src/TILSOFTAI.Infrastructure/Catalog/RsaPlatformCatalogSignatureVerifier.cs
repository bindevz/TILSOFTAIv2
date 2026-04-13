using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Infrastructure.Catalog;

public sealed class RsaPlatformCatalogSignatureVerifier : IPlatformCatalogSignatureVerifier
{
    private readonly CatalogCertificationOptions _options;

    public RsaPlatformCatalogSignatureVerifier(IOptions<CatalogCertificationOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
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

        var signer = _options.TrustedEvidenceSigners.FirstOrDefault(item =>
            string.Equals(item.SignerId, evidence.SignerId, StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(evidence.SignerPublicKeyId)
                || string.Equals(item.KeyId, evidence.SignerPublicKeyId, StringComparison.OrdinalIgnoreCase)));
        if (signer is null)
        {
            errors.Add("signature_signer_not_trusted");
        }

        if (errors.Count > 0)
        {
            return Failed(evidence, algorithm, errors);
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
                return Failed(evidence, algorithm, new[] { "signature_invalid" });
            }

            return new CatalogEvidenceSignatureVerificationResult
            {
                WasSignaturePresent = true,
                IsVerified = true,
                SignerId = signer.SignerId,
                SignerPublicKeyId = signer.KeyId,
                SignatureAlgorithm = algorithm,
                SignatureVerifiedAtUtc = utcNow
            };
        }
        catch (FormatException)
        {
            return Failed(evidence, algorithm, new[] { "signature_base64_invalid" });
        }
        catch (CryptographicException)
        {
            return Failed(evidence, algorithm, new[] { "signature_key_invalid" });
        }
    }

    private static CatalogEvidenceSignatureVerificationResult Failed(
        CatalogCertificationEvidenceRecord evidence,
        string algorithm,
        IReadOnlyList<string> errors) => new()
        {
            WasSignaturePresent = true,
            IsVerified = false,
            SignerId = evidence.SignerId,
            SignerPublicKeyId = evidence.SignerPublicKeyId,
            SignatureAlgorithm = algorithm,
            Errors = errors
        };
}
