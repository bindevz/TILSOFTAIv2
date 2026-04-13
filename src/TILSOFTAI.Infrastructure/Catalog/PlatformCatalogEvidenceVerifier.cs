using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Infrastructure.Catalog;

public sealed partial class PlatformCatalogEvidenceVerifier : IPlatformCatalogEvidenceVerifier
{
    private readonly CatalogCertificationOptions _options;
    private readonly IPlatformCatalogArtifactProvider _artifactProvider;
    private readonly IPlatformCatalogSignatureVerifier _signatureVerifier;

    public PlatformCatalogEvidenceVerifier(
        IOptions<CatalogCertificationOptions> options,
        IPlatformCatalogArtifactProvider artifactProvider)
        : this(options, artifactProvider, new RsaPlatformCatalogSignatureVerifier(options))
    {
    }

    public PlatformCatalogEvidenceVerifier(
        IOptions<CatalogCertificationOptions> options,
        IPlatformCatalogArtifactProvider artifactProvider,
        IPlatformCatalogSignatureVerifier signatureVerifier)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _artifactProvider = artifactProvider ?? throw new ArgumentNullException(nameof(artifactProvider));
        _signatureVerifier = signatureVerifier ?? throw new ArgumentNullException(nameof(signatureVerifier));
    }

    public CatalogEvidenceVerificationResult Verify(
        CatalogCertificationEvidenceRecord evidence,
        CatalogMutationContext context,
        bool acceptAsTrusted,
        string verificationNotes)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(context);

        var now = DateTime.UtcNow;
        var errors = ValidateEvidence(evidence, now);
        var artifactVerification = _artifactProvider.Verify(evidence);
        if (artifactVerification.WasProviderControlled && !artifactVerification.IsVerified)
        {
            errors.AddRange(artifactVerification.Errors);
        }

        var signatureVerification = _signatureVerifier.Verify(evidence, now);
        if (signatureVerification.WasSignaturePresent && !signatureVerification.IsVerified)
        {
            errors.AddRange(signatureVerification.Errors);
        }

        var verified = errors.Count == 0;
        var trustTier = verified
            ? TrustTier(artifactVerification, signatureVerification)
            : string.Empty;
        var verificationMethod = verified
            ? VerificationMethod(artifactVerification, signatureVerification)
            : string.Empty;
        var notes = string.Join("; ", errors);
        if (!string.IsNullOrWhiteSpace(verificationNotes))
        {
            notes = string.IsNullOrWhiteSpace(notes)
                ? verificationNotes.Trim()
                : $"{notes}; {verificationNotes.Trim()}";
        }

        return new CatalogEvidenceVerificationResult
        {
            IsVerified = verified,
            EvidenceId = evidence.EvidenceId,
            Status = verified && acceptAsTrusted
                ? CatalogCertificationEvidenceStatus.Accepted
                : verified
                    ? CatalogCertificationEvidenceStatus.Verified
                    : CatalogCertificationEvidenceStatus.Recorded,
            VerificationStatus = verified
                ? CatalogEvidenceVerificationStatus.Verified
                : CatalogEvidenceVerificationStatus.Failed,
            VerificationNotes = notes,
            VerifiedByUserId = context.UserId,
            VerifiedAtUtc = now,
            ExpiresAtUtc = verified && evidence.CollectedAtUtc.HasValue && _options.MaxTrustedEvidenceAgeDays > 0
                ? evidence.CollectedAtUtc.Value.AddDays(FreshnessDays(evidence))
                : evidence.ExpiresAtUtc,
            TrustTier = trustTier,
            ArtifactProvider = artifactVerification.ProviderName,
            ProviderVerifiedAtUtc = artifactVerification.IsVerified ? now : null,
            ArtifactSizeBytes = artifactVerification.ArtifactSizeBytes,
            SignerId = signatureVerification.SignerId,
            SignerPublicKeyId = signatureVerification.SignerPublicKeyId,
            SignatureVerifiedAtUtc = signatureVerification.SignatureVerifiedAtUtc,
            VerificationMethod = verificationMethod,
            VerificationPolicyVersion = _options.PolicyVersion,
            Errors = errors
        };
    }

    public bool IsTrusted(CatalogCertificationEvidenceRecord evidence, DateTime utcNow) =>
        EvaluateTrust(evidence, utcNow).IsTrusted;

    public CatalogEvidenceTrustEvaluation EvaluateTrust(CatalogCertificationEvidenceRecord evidence, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        var failures = new List<string>();
        var requiredTrustTier = RequiredTrustTier(evidence.EnvironmentName);
        var trustTier = string.IsNullOrWhiteSpace(evidence.TrustTier)
            ? CatalogEvidenceTrustTiers.MetadataVerified
            : evidence.TrustTier;
        var freshUntil = evidence.CollectedAtUtc?.AddDays(FreshnessDays(evidence));

        if (!string.Equals(evidence.VerificationStatus, CatalogEvidenceVerificationStatus.Verified, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("evidence_not_verified");
        }

        if (!_options.TrustedEvidenceStatuses.Any(status => string.Equals(status, evidence.Status, StringComparison.OrdinalIgnoreCase)))
        {
            failures.Add("evidence_status_not_trusted");
        }

        if (evidence.ExpiresAtUtc.HasValue && evidence.ExpiresAtUtc.Value <= utcNow)
        {
            failures.Add("evidence_expired");
        }

        if (string.Equals(evidence.Status, CatalogCertificationEvidenceStatus.Expired, StringComparison.OrdinalIgnoreCase)
            || string.Equals(evidence.Status, CatalogCertificationEvidenceStatus.Superseded, StringComparison.OrdinalIgnoreCase)
            || string.Equals(evidence.VerificationStatus, CatalogEvidenceVerificationStatus.Expired, StringComparison.OrdinalIgnoreCase)
            || string.Equals(evidence.VerificationStatus, CatalogEvidenceVerificationStatus.Superseded, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("evidence_lifecycle_not_current");
        }

        if (CatalogEvidenceTrustTiers.Rank(trustTier) < CatalogEvidenceTrustTiers.Rank(requiredTrustTier))
        {
            failures.Add($"evidence_trust_tier_insufficient:{trustTier}:{requiredTrustTier}");
        }

        if (freshUntil.HasValue && freshUntil.Value <= utcNow)
        {
            failures.Add($"evidence_freshness_expired:{evidence.EvidenceKind}");
        }

        failures.AddRange(ValidateEvidence(evidence, utcNow));

        return new CatalogEvidenceTrustEvaluation
        {
            IsTrusted = failures.Count == 0,
            EvidenceId = evidence.EvidenceId,
            TrustTier = trustTier,
            RequiredTrustTier = requiredTrustTier,
            IsFresh = !freshUntil.HasValue || freshUntil.Value > utcNow,
            FreshUntilUtc = freshUntil,
            VerificationMethod = evidence.VerificationMethod,
            VerificationPolicyVersion = evidence.VerificationPolicyVersion,
            Failures = failures.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }

    private List<string> ValidateEvidence(CatalogCertificationEvidenceRecord evidence, DateTime utcNow)
    {
        var errors = new List<string>();

        if (_options.RequireEvidenceUriForTrustedEvidence && string.IsNullOrWhiteSpace(evidence.EvidenceUri))
        {
            errors.Add("evidence_uri_required");
        }

        if (!string.IsNullOrWhiteSpace(evidence.EvidenceUri)
            && _options.AllowedEvidenceUriPrefixes.Length > 0
            && !_options.AllowedEvidenceUriPrefixes.Any(prefix => evidence.EvidenceUri.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add("evidence_uri_not_allowed");
        }

        if (_options.RequireArtifactHashForTrustedEvidence && string.IsNullOrWhiteSpace(evidence.ArtifactHash))
        {
            errors.Add("artifact_hash_required");
        }

        if (!string.IsNullOrWhiteSpace(evidence.ArtifactHash)
            && !Sha256HexRegex().IsMatch(evidence.ArtifactHash))
        {
            errors.Add("artifact_hash_invalid");
        }

        if (!string.IsNullOrWhiteSpace(evidence.ArtifactHashAlgorithm)
            && !string.Equals(evidence.ArtifactHashAlgorithm, "sha256", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("artifact_hash_algorithm_unsupported");
        }

        if (!string.IsNullOrWhiteSpace(evidence.ArtifactContentType)
            && _options.AllowedEvidenceContentTypes.Length > 0
            && !_options.AllowedEvidenceContentTypes.Any(contentType => string.Equals(contentType, evidence.ArtifactContentType, StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add("artifact_content_type_not_allowed");
        }

        if (!string.IsNullOrWhiteSpace(evidence.SourceSystem)
            && _options.AllowedEvidenceSourceSystems.Length > 0
            && !_options.AllowedEvidenceSourceSystems.Any(source => string.Equals(source, evidence.SourceSystem, StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add("source_system_not_allowed");
        }

        if (!evidence.CollectedAtUtc.HasValue)
        {
            errors.Add("collected_at_required");
        }
        else
        {
            if (evidence.CollectedAtUtc.Value > utcNow.AddMinutes(5))
            {
                errors.Add("collected_at_in_future");
            }

            if (FreshnessDays(evidence) > 0
                && evidence.CollectedAtUtc.Value < utcNow.AddDays(-FreshnessDays(evidence)))
            {
                errors.Add("evidence_stale");
            }
        }

        return errors;
    }

    private int FreshnessDays(CatalogCertificationEvidenceRecord evidence) =>
        _options.EvidenceFreshnessDaysByKind.TryGetValue(evidence.EvidenceKind, out var days)
            ? days
            : _options.MaxTrustedEvidenceAgeDays;

    private string RequiredTrustTier(string environmentName) =>
        _options.EnvironmentMinimumEvidenceTrustTiers.TryGetValue(environmentName, out var tier)
            ? tier
            : _options.ProductionLikeEnvironments.Any(item => string.Equals(item, environmentName, StringComparison.OrdinalIgnoreCase))
                ? _options.MinimumEvidenceTrustTierForProductionLikePromotion
                : CatalogEvidenceTrustTiers.MetadataVerified;

    private static string TrustTier(
        CatalogArtifactVerificationResult artifactVerification,
        CatalogEvidenceSignatureVerificationResult signatureVerification)
    {
        if (signatureVerification.IsVerified)
        {
            return CatalogEvidenceTrustTiers.SignatureVerified;
        }

        if (artifactVerification.IsVerified)
        {
            return CatalogEvidenceTrustTiers.ProviderVerified;
        }

        return CatalogEvidenceTrustTiers.MetadataVerified;
    }

    private static string VerificationMethod(
        CatalogArtifactVerificationResult artifactVerification,
        CatalogEvidenceSignatureVerificationResult signatureVerification)
    {
        if (signatureVerification.IsVerified)
        {
            return CatalogEvidenceVerificationMethods.Signature;
        }

        if (artifactVerification.IsVerified)
        {
            return CatalogEvidenceVerificationMethods.Provider;
        }

        return CatalogEvidenceVerificationMethods.Metadata;
    }

    [GeneratedRegex("^[a-fA-F0-9]{64}$")]
    private static partial Regex Sha256HexRegex();
}
