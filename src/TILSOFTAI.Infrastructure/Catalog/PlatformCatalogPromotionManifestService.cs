using System.Text.Json;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Infrastructure.Catalog;

public sealed class PlatformCatalogPromotionManifestService : IPlatformCatalogPromotionManifestService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IPlatformCatalogPromotionGate _promotionGate;
    private readonly IPlatformCatalogCertificationStore _certificationStore;
    private readonly IPlatformCatalogMutationStore _mutationStore;
    private readonly IPlatformCatalogPromotionManifestStore _manifestStore;
    private readonly IPlatformCatalogEvidenceVerifier _evidenceVerifier;
    private readonly IPlatformCatalogDossierArchiveService _archiveService;
    private readonly CatalogCertificationOptions _options;

    public PlatformCatalogPromotionManifestService(
        IPlatformCatalogPromotionGate promotionGate,
        IPlatformCatalogCertificationStore certificationStore,
        IPlatformCatalogMutationStore mutationStore,
        IPlatformCatalogPromotionManifestStore manifestStore,
        IPlatformCatalogEvidenceVerifier evidenceVerifier,
        IOptions<CatalogCertificationOptions> options)
        : this(
            promotionGate,
            certificationStore,
            mutationStore,
            manifestStore,
            evidenceVerifier,
            new FileSystemPlatformCatalogDossierArchiveService(options),
            options)
    {
    }

    public PlatformCatalogPromotionManifestService(
        IPlatformCatalogPromotionGate promotionGate,
        IPlatformCatalogCertificationStore certificationStore,
        IPlatformCatalogMutationStore mutationStore,
        IPlatformCatalogPromotionManifestStore manifestStore,
        IPlatformCatalogEvidenceVerifier evidenceVerifier,
        IPlatformCatalogDossierArchiveService archiveService,
        IOptions<CatalogCertificationOptions> options)
    {
        _promotionGate = promotionGate ?? throw new ArgumentNullException(nameof(promotionGate));
        _certificationStore = certificationStore ?? throw new ArgumentNullException(nameof(certificationStore));
        _mutationStore = mutationStore ?? throw new ArgumentNullException(nameof(mutationStore));
        _manifestStore = manifestStore ?? throw new ArgumentNullException(nameof(manifestStore));
        _evidenceVerifier = evidenceVerifier ?? throw new ArgumentNullException(nameof(evidenceVerifier));
        _archiveService = archiveService ?? throw new ArgumentNullException(nameof(archiveService));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<CatalogPromotionManifestIssueResult> IssueManifestAsync(
        CatalogPromotionManifestIssueRequest request,
        CatalogMutationContext context,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var blockers = new List<string>();
        var environment = EffectiveEnvironmentName(request.EnvironmentName);
        var productionLike = IsProductionLike(environment);
        var changeIds = CleanDistinct(request.ChangeIds);
        var evidenceIds = CleanDistinct(request.EvidenceIds);
        var trustedEvidence = await LoadTrustedEvidenceAsync(evidenceIds, environment, blockers, ct);

        if (changeIds.Count == 0)
        {
            blockers.Add("manifest_change_ids_required");
        }

        if (productionLike && _options.RequireCertificationEvidenceForProductionLikePromotion)
        {
            if (evidenceIds.Count == 0)
            {
                blockers.Add("manifest_evidence_ids_required");
            }

            var evidenceKinds = trustedEvidence.Select(item => item.EvidenceKind).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var required in _options.RequiredEvidenceKinds.Where(required => !evidenceKinds.Contains(required)))
            {
                blockers.Add($"manifest_required_evidence_missing:{required}");
            }
        }

        var gateResults = new List<CatalogPromotionGateResult>();
        foreach (var changeId in changeIds)
        {
            var gate = await _promotionGate.EvaluateAsync(new CatalogPromotionGateRequest
            {
                EnvironmentName = environment,
                ChangeId = changeId,
                IncludeCertificationEvidence = true
            }, context, ct);
            gateResults.Add(gate);

            if (!gate.IsAllowed)
            {
                blockers.AddRange(gate.Blockers.Select(blocker => $"manifest_gate_blocked:{changeId}:{blocker}"));
            }
        }

        if (blockers.Count > 0)
        {
            return new CatalogPromotionManifestIssueResult
            {
                IsIssued = false,
                Blockers = blockers.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                GateResults = gateResults
            };
        }

        var now = DateTime.UtcNow;
        var manifest = new CatalogPromotionManifestRecord
        {
            ManifestId = Guid.NewGuid().ToString("N"),
            EnvironmentName = environment,
            Status = CatalogPromotionManifestStatus.Issued,
            ChangeIds = changeIds,
            EvidenceIds = evidenceIds,
            GateSummaryJson = JsonSerializer.Serialize(gateResults.Select(ToGateSummary).ToArray(), JsonOptions),
            RollbackOfManifestId = request.RollbackOfManifestId.Trim(),
            RelatedIncidentId = request.RelatedIncidentId.Trim(),
            Notes = request.Notes.Trim(),
            CreatedByUserId = context.UserId,
            IssuedByUserId = context.UserId,
            CorrelationId = context.CorrelationId,
            CreatedAtUtc = now,
            IssuedAtUtc = now
        };

        manifest = manifest with { ManifestHash = PlatformCatalogPromotionManifestHasher.ComputeHash(manifest) };
        var created = await _manifestStore.CreateManifestAsync(manifest, ct);
        await _manifestStore.CreateAttestationAsync(new CatalogRolloutAttestationRecord
        {
            AttestationId = Guid.NewGuid().ToString("N"),
            ManifestId = created.ManifestId,
            EnvironmentName = created.EnvironmentName,
            State = CatalogRolloutAttestationStates.Issued,
            Notes = "promotion manifest issued",
            EvidenceIds = evidenceIds,
            ActorUserId = context.UserId,
            CorrelationId = context.CorrelationId,
            CreatedAtUtc = now
        }, ct);

        return new CatalogPromotionManifestIssueResult
        {
            IsIssued = true,
            Manifest = created,
            GateResults = gateResults
        };
    }

    public async Task<CatalogRolloutAttestationResult> RecordAttestationAsync(
        string manifestId,
        CatalogRolloutAttestationRequest request,
        CatalogMutationContext context,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var blockers = new List<string>();
        var manifest = await _manifestStore.GetManifestAsync(manifestId, ct);
        if (manifest is null)
        {
            return new CatalogRolloutAttestationResult
            {
                IsRecorded = false,
                Blockers = new[] { "manifest_not_found" }
            };
        }

        var state = request.State.Trim().ToLowerInvariant();
        if (!IsKnownAttestationState(state))
        {
            blockers.Add("rollout_state_invalid");
        }

        var productionLike = IsProductionLike(manifest.EnvironmentName);
        var evidenceIds = CleanDistinct(request.EvidenceIds);
        if (productionLike
            && string.Equals(state, CatalogRolloutAttestationStates.Completed, StringComparison.OrdinalIgnoreCase)
            && _options.RequireRolloutAttestationEvidenceForProductionLikeCompletion)
        {
            if (evidenceIds.Count == 0)
            {
                blockers.Add("rollout_completion_evidence_required");
            }

            await LoadTrustedEvidenceAsync(evidenceIds, manifest.EnvironmentName, blockers, ct);
        }

        if (productionLike
            && string.Equals(state, CatalogRolloutAttestationStates.Completed, StringComparison.OrdinalIgnoreCase)
            && _options.RequireArchivedDossierForProductionLikeCompletion
            && await _archiveService.GetArchiveAsync(manifest.ManifestId, ct) is null)
        {
            blockers.Add("dossier_archive_required");
        }

        if (blockers.Count > 0)
        {
            return new CatalogRolloutAttestationResult
            {
                IsRecorded = false,
                Blockers = blockers.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            };
        }

        var attestation = await _manifestStore.CreateAttestationAsync(new CatalogRolloutAttestationRecord
        {
            AttestationId = Guid.NewGuid().ToString("N"),
            ManifestId = manifest.ManifestId,
            EnvironmentName = manifest.EnvironmentName,
            State = state,
            Notes = request.Notes.Trim(),
            EvidenceIds = evidenceIds,
            ActorUserId = context.UserId,
            AcceptedByUserId = request.AcceptedByUserId.Trim(),
            CorrelationId = context.CorrelationId,
            CreatedAtUtc = DateTime.UtcNow
        }, ct);

        return new CatalogRolloutAttestationResult
        {
            IsRecorded = true,
            Attestation = attestation
        };
    }

    public async Task<CatalogPromotionDossier?> GetDossierAsync(
        string manifestId,
        CatalogMutationContext context,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        var manifest = await _manifestStore.GetManifestAsync(manifestId, ct);
        if (manifest is null)
        {
            return null;
        }

        var changes = new List<CatalogChangeRequestRecord>();
        foreach (var changeId in manifest.ChangeIds)
        {
            var change = await _mutationStore.GetChangeAsync(context.TenantId, changeId, ct);
            if (change is not null)
            {
                changes.Add(change);
            }
        }

        var evidence = new List<CatalogCertificationEvidenceRecord>();
        foreach (var evidenceId in manifest.EvidenceIds)
        {
            var item = await _certificationStore.GetEvidenceAsync(evidenceId, ct);
            if (item is not null)
            {
                evidence.Add(item);
            }
        }

        var attestations = await _manifestStore.ListAttestationsAsync(manifest.ManifestId, ct);
        var evidenceTrust = evidence.Select(item => _evidenceVerifier.EvaluateTrust(item, DateTime.UtcNow)).ToArray();
        var retention = RetentionSnapshot(manifest, evidence, attestations);
        var warnings = new List<string>();
        if (!string.Equals(manifest.ManifestHash, PlatformCatalogPromotionManifestHasher.ComputeHash(manifest), StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("manifest_hash_mismatch");
        }

        if (manifest.ChangeIds.Count != changes.Count)
        {
            warnings.Add("manifest_change_record_missing");
        }

        if (manifest.EvidenceIds.Count != evidence.Count)
        {
            warnings.Add("manifest_evidence_record_missing");
        }

        warnings.AddRange(evidenceTrust
            .Where(item => !item.IsTrusted)
            .Select(item => $"dossier_evidence_not_trusted:{item.EvidenceId}"));
        warnings.AddRange(evidenceTrust
            .SelectMany(item => item.Warnings.Select(warning => $"dossier_evidence_trust_warning:{item.EvidenceId}:{warning}")));

        if (!retention.RetentionCurrent)
        {
            warnings.Add("dossier_retention_window_expired");
        }

        var archive = await _archiveService.GetArchiveAsync(manifest.ManifestId, ct);
        CatalogDossierArchiveVerificationResult? archiveVerification = null;
        if (retention.ArchiveRequired && archive is null)
        {
            warnings.Add("dossier_archive_required");
        }
        else if (archive is not null)
        {
            archiveVerification = await _archiveService.VerifyArchiveAsync(manifest.ManifestId, ct);
            if (!archiveVerification.IsVerified)
            {
                warnings.AddRange(archiveVerification.Errors.Select(error => $"dossier_archive_verification_failed:{error}"));
            }
        }

        var dossier = new CatalogPromotionDossier
        {
            Manifest = manifest,
            Changes = changes,
            Evidence = evidence,
            EvidenceTrust = evidenceTrust,
            Attestations = attestations,
            Retention = retention,
            Archive = archive,
            ArchiveVerification = archiveVerification,
            AuditWarnings = warnings,
            GeneratedAtUtc = DateTime.UtcNow
        };
        dossier = dossier with { DossierHash = PlatformCatalogPromotionManifestHasher.ComputeDossierHash(dossier) };
        if (archive is not null && !string.Equals(archive.DossierHash, dossier.DossierHash, StringComparison.OrdinalIgnoreCase))
        {
            dossier = dossier with { AuditWarnings = dossier.AuditWarnings.Concat(new[] { "dossier_archive_hash_mismatch" }).ToArray() };
        }

        return dossier;
    }

    public async Task<CatalogDossierArchiveResult> ArchiveDossierAsync(
        string manifestId,
        CatalogMutationContext context,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        var dossier = await GetDossierAsync(manifestId, context, ct);
        if (dossier is null)
        {
            return new CatalogDossierArchiveResult
            {
                IsArchived = false,
                Blockers = new[] { "manifest_not_found" }
            };
        }

        var blockers = new List<string>();
        if (!dossier.Retention.RetentionCurrent)
        {
            blockers.Add("dossier_retention_window_expired");
        }

        if (dossier.EvidenceTrust.Any(item => !item.IsTrusted))
        {
            blockers.AddRange(dossier.EvidenceTrust
                .Where(item => !item.IsTrusted)
                .Select(item => $"dossier_evidence_not_trusted:{item.EvidenceId}"));
        }

        if (blockers.Count > 0)
        {
            return new CatalogDossierArchiveResult
            {
                IsArchived = false,
                Blockers = blockers.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            };
        }

        var archive = await _archiveService.ArchiveAsync(dossier, context, ct);
        return new CatalogDossierArchiveResult
        {
            IsArchived = true,
            Archive = archive
        };
    }

    public Task<CatalogDossierArchiveVerificationResult> VerifyDossierArchiveAsync(
        string manifestId,
        CatalogMutationContext context,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _archiveService.VerifyArchiveAsync(manifestId, ct);
    }

    private CatalogAuditRetentionSnapshot RetentionSnapshot(
        CatalogPromotionManifestRecord manifest,
        IReadOnlyList<CatalogCertificationEvidenceRecord> evidence,
        IReadOnlyList<CatalogRolloutAttestationRecord> attestations)
    {
        var manifestUntil = _options.ManifestRetentionDays > 0 ? manifest.IssuedAtUtc.AddDays(_options.ManifestRetentionDays) : (DateTime?)null;
        var evidenceUntil = _options.EvidenceRetentionDays > 0 && evidence.Count > 0
            ? evidence.Min(item => item.CapturedAtUtc).AddDays(_options.EvidenceRetentionDays)
            : (DateTime?)null;
        var attestationUntil = _options.AttestationRetentionDays > 0 && attestations.Count > 0
            ? attestations.Min(item => item.CreatedAtUtc).AddDays(_options.AttestationRetentionDays)
            : (DateTime?)null;
        var dossierUntil = _options.DossierArchiveRetentionDays > 0 ? manifest.IssuedAtUtc.AddDays(_options.DossierArchiveRetentionDays) : (DateTime?)null;
        var now = DateTime.UtcNow;
        var retainUntilValues = new[] { manifestUntil, evidenceUntil, attestationUntil, dossierUntil }.Where(item => item.HasValue).Select(item => item!.Value);

        return new CatalogAuditRetentionSnapshot
        {
            PolicyVersion = _options.PolicyVersion,
            ManifestRetainUntilUtc = manifestUntil,
            EvidenceRetainUntilUtc = evidenceUntil,
            AttestationRetainUntilUtc = attestationUntil,
            DossierArchiveRetainUntilUtc = dossierUntil,
            ArchiveRequired = _options.RequireArchiveForProductionLikeDossiers && IsProductionLike(manifest.EnvironmentName),
            RetentionCurrent = !retainUntilValues.Any(item => item <= now)
        };
    }

    private async Task<List<CatalogCertificationEvidenceRecord>> LoadTrustedEvidenceAsync(
        IReadOnlyList<string> evidenceIds,
        string environment,
        List<string> blockers,
        CancellationToken ct)
    {
        var trusted = new List<CatalogCertificationEvidenceRecord>();
        foreach (var evidenceId in evidenceIds)
        {
            var evidence = await _certificationStore.GetEvidenceAsync(evidenceId, ct);
            if (evidence is null)
            {
                blockers.Add($"evidence_not_found:{evidenceId}");
                continue;
            }

            if (!string.Equals(evidence.EnvironmentName, environment, StringComparison.OrdinalIgnoreCase))
            {
                blockers.Add($"evidence_environment_mismatch:{evidenceId}");
                continue;
            }

            if (!_evidenceVerifier.IsTrusted(evidence, DateTime.UtcNow))
            {
                blockers.Add($"evidence_untrusted:{evidenceId}");
                var trust = _evidenceVerifier.EvaluateTrust(evidence, DateTime.UtcNow);
                blockers.AddRange(trust.Failures.Select(failure => $"evidence_trust_failure:{evidenceId}:{failure}"));
                continue;
            }

            trusted.Add(evidence);
        }

        return trusted;
    }

    private static object ToGateSummary(CatalogPromotionGateResult gate) => new
    {
        gate.IsAllowed,
        gate.EnvironmentName,
        gate.SourceMode,
        gate.ChangeId,
        gate.RiskLevel,
        gate.Blockers,
        gate.Warnings,
        gate.EvidenceMissing,
        gate.EvidenceUntrusted,
        gate.EvidenceTrustFailures
    };

    private static List<string> CleanDistinct(IReadOnlyList<string> values) =>
        values
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private bool IsProductionLike(string environment) =>
        _options.ProductionLikeEnvironments.Any(item => string.Equals(item, environment, StringComparison.OrdinalIgnoreCase));

    private string EffectiveEnvironmentName(string requestedEnvironment) =>
        !string.IsNullOrWhiteSpace(requestedEnvironment)
            ? requestedEnvironment.Trim()
            : !string.IsNullOrWhiteSpace(_options.EnvironmentName)
                ? _options.EnvironmentName.Trim()
                : "development";

    private static bool IsKnownAttestationState(string state) =>
        string.Equals(state, CatalogRolloutAttestationStates.Started, StringComparison.OrdinalIgnoreCase)
        || string.Equals(state, CatalogRolloutAttestationStates.Completed, StringComparison.OrdinalIgnoreCase)
        || string.Equals(state, CatalogRolloutAttestationStates.Failed, StringComparison.OrdinalIgnoreCase)
        || string.Equals(state, CatalogRolloutAttestationStates.Aborted, StringComparison.OrdinalIgnoreCase)
        || string.Equals(state, CatalogRolloutAttestationStates.Superseded, StringComparison.OrdinalIgnoreCase);
}
