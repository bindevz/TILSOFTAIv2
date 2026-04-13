using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Infrastructure.Catalog;

public sealed partial class PlatformCatalogEvidenceVerifier : IPlatformCatalogEvidenceVerifier
{
    private readonly CatalogCertificationOptions _options;

    public PlatformCatalogEvidenceVerifier(IOptions<CatalogCertificationOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
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
        var verified = errors.Count == 0;
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
                ? evidence.CollectedAtUtc.Value.AddDays(_options.MaxTrustedEvidenceAgeDays)
                : evidence.ExpiresAtUtc,
            Errors = errors
        };
    }

    public bool IsTrusted(CatalogCertificationEvidenceRecord evidence, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        if (!string.Equals(evidence.VerificationStatus, CatalogEvidenceVerificationStatus.Verified, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!_options.TrustedEvidenceStatuses.Any(status => string.Equals(status, evidence.Status, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (evidence.ExpiresAtUtc.HasValue && evidence.ExpiresAtUtc.Value <= utcNow)
        {
            return false;
        }

        if (string.Equals(evidence.Status, CatalogCertificationEvidenceStatus.Expired, StringComparison.OrdinalIgnoreCase)
            || string.Equals(evidence.Status, CatalogCertificationEvidenceStatus.Superseded, StringComparison.OrdinalIgnoreCase)
            || string.Equals(evidence.VerificationStatus, CatalogEvidenceVerificationStatus.Expired, StringComparison.OrdinalIgnoreCase)
            || string.Equals(evidence.VerificationStatus, CatalogEvidenceVerificationStatus.Superseded, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return ValidateEvidence(evidence, utcNow).Count == 0;
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

            if (_options.MaxTrustedEvidenceAgeDays > 0
                && evidence.CollectedAtUtc.Value < utcNow.AddDays(-_options.MaxTrustedEvidenceAgeDays))
            {
                errors.Add("evidence_stale");
            }
        }

        return errors;
    }

    [GeneratedRegex("^[a-fA-F0-9]{64}$")]
    private static partial Regex Sha256HexRegex();
}
