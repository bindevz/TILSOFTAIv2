using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Metrics;

namespace TILSOFTAI.Infrastructure.Catalog;

public sealed class PlatformCatalogPromotionGate : IPlatformCatalogPromotionGate
{
    private readonly IPlatformCatalogProvider _catalogProvider;
    private readonly IPlatformCatalogControlPlane _controlPlane;
    private readonly IPlatformCatalogMutationStore _mutationStore;
    private readonly IPlatformCatalogCertificationStore _certificationStore;
    private readonly IPlatformCatalogEvidenceVerifier _evidenceVerifier;
    private readonly IConfiguration _configuration;
    private readonly PlatformCatalogOptions _catalogOptions;
    private readonly CatalogControlPlaneOptions _controlPlaneOptions;
    private readonly CatalogCertificationOptions _certificationOptions;
    private readonly IMetricsService _metrics;

    public PlatformCatalogPromotionGate(
        IPlatformCatalogProvider catalogProvider,
        IPlatformCatalogControlPlane controlPlane,
        IPlatformCatalogMutationStore mutationStore,
        IPlatformCatalogCertificationStore certificationStore,
        IPlatformCatalogEvidenceVerifier evidenceVerifier,
        IConfiguration configuration,
        IOptions<PlatformCatalogOptions> catalogOptions,
        IOptions<CatalogControlPlaneOptions> controlPlaneOptions,
        IOptions<CatalogCertificationOptions> certificationOptions,
        IMetricsService metrics)
    {
        _catalogProvider = catalogProvider ?? throw new ArgumentNullException(nameof(catalogProvider));
        _controlPlane = controlPlane ?? throw new ArgumentNullException(nameof(controlPlane));
        _mutationStore = mutationStore ?? throw new ArgumentNullException(nameof(mutationStore));
        _certificationStore = certificationStore ?? throw new ArgumentNullException(nameof(certificationStore));
        _evidenceVerifier = evidenceVerifier ?? throw new ArgumentNullException(nameof(evidenceVerifier));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _catalogOptions = catalogOptions?.Value ?? throw new ArgumentNullException(nameof(catalogOptions));
        _controlPlaneOptions = controlPlaneOptions?.Value ?? throw new ArgumentNullException(nameof(controlPlaneOptions));
        _certificationOptions = certificationOptions?.Value ?? throw new ArgumentNullException(nameof(certificationOptions));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
    }

    public async Task<CatalogPromotionGateResult> EvaluateAsync(
        CatalogPromotionGateRequest request,
        CatalogMutationContext context,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var environment = EffectiveEnvironmentName(request.EnvironmentName);
        var productionLike = IsProductionLike(environment);
        var blockers = new List<string>();
        var warnings = new List<string>();
        var missingEvidence = new List<string>();
        var untrustedEvidence = new List<string>();
        var evidenceTrustFailures = new List<string>();
        var sourceMode = DetermineSourceMode();
        CatalogMutationPreviewResult? preview = null;
        CatalogChangeRequestRecord? change = null;

        if (!_catalogProvider.Load().IsValid)
        {
            blockers.Add("catalog_integrity_invalid");
        }

        if (sourceMode == "empty")
        {
            blockers.Add("catalog_source_empty");
        }

        if (productionLike && sourceMode == "mixed" && _catalogOptions.TreatMixedAsUnhealthyInProductionLike)
        {
            blockers.Add("production_mixed_source_mode_blocked");
        }

        if (productionLike && sourceMode == "bootstrap_only" && _catalogOptions.TreatBootstrapOnlyAsUnhealthyInProductionLike)
        {
            blockers.Add("production_bootstrap_only_source_mode_blocked");
        }

        if (request.MutationPreview is not null)
        {
            preview = await _controlPlane.PreviewAsync(request.MutationPreview, context, ct);
            if (!preview.IsValid)
            {
                blockers.Add("catalog_preview_failed");
            }

            if (productionLike && preview.ExistingRecordFound && string.IsNullOrWhiteSpace(preview.ExpectedVersionTag))
            {
                blockers.Add("catalog_expected_version_required");
            }

            warnings.AddRange(preview.Warnings);
        }

        if (!string.IsNullOrWhiteSpace(request.ChangeId))
        {
            change = await _mutationStore.GetChangeAsync(context.TenantId, request.ChangeId, ct);
            if (change is null)
            {
                blockers.Add("catalog_change_not_found");
            }
            else
            {
                await ValidateChangeForPromotionAsync(change, productionLike, blockers, warnings, ct);
            }
        }

        if (productionLike
            && request.IncludeCertificationEvidence
            && _certificationOptions.RequireCertificationEvidenceForProductionLikePromotion)
        {
            var evidence = await _certificationStore.ListEvidenceAsync(environment, ct);
            var trustEvaluations = evidence
                .Select(item => _evidenceVerifier.EvaluateTrust(item, DateTime.UtcNow))
                .ToDictionary(item => item.EvidenceId, StringComparer.OrdinalIgnoreCase);
            var trustedEvidence = evidence
                .Where(item => !_certificationOptions.RequireTrustedEvidenceForProductionLikePromotion
                    || (trustEvaluations.TryGetValue(item.EvidenceId, out var trust) && trust.IsTrusted))
                .ToArray();
            var acceptedKinds = trustedEvidence
                .Select(item => item.EvidenceKind)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            untrustedEvidence.AddRange(evidence
                .Where(item => _certificationOptions.RequiredEvidenceKinds.Contains(item.EvidenceKind, StringComparer.OrdinalIgnoreCase))
                .Where(item => _certificationOptions.RequireTrustedEvidenceForProductionLikePromotion
                    && trustEvaluations.TryGetValue(item.EvidenceId, out var trust)
                    && !trust.IsTrusted)
                .Select(item => item.EvidenceId));
            evidenceTrustFailures.AddRange(trustEvaluations.Values
                .Where(item => !item.IsTrusted)
                .SelectMany(item => item.Failures.Select(failure => $"{item.EvidenceId}:{failure}")));

            missingEvidence.AddRange(_certificationOptions.RequiredEvidenceKinds
                .Where(required => !acceptedKinds.Contains(required)));

            if (missingEvidence.Count > 0)
            {
                blockers.Add("catalog_certification_evidence_missing");
            }
        }

        var result = new CatalogPromotionGateResult
        {
            IsAllowed = blockers.Count == 0,
            EnvironmentName = environment,
            ProductionLike = productionLike,
            SourceMode = sourceMode,
            ChangeId = request.ChangeId,
            RiskLevel = change?.RiskLevel ?? preview?.RiskLevel ?? string.Empty,
            Blockers = blockers,
            Warnings = warnings,
            EvidenceMissing = missingEvidence,
            EvidenceUntrusted = untrustedEvidence,
            EvidenceTrustFailures = evidenceTrustFailures,
            Preview = preview
        };

        _metrics.IncrementCounter(MetricNames.PlatformCatalogPromotionGateTotal, new Dictionary<string, string>
        {
            ["environment"] = environment,
            ["source_mode"] = sourceMode,
            ["production_like"] = productionLike ? "true" : "false",
            ["allowed"] = result.IsAllowed ? "true" : "false"
        });

        return result;
    }

    public IReadOnlyList<CatalogControlPlaneSloDefinition> GetSloDefinitions() => new[]
    {
        new CatalogControlPlaneSloDefinition
        {
            Name = "catalog_preview_success",
            Target = $">= {_certificationOptions.PreviewSuccessSloPercent}% successful previews per rolling hour",
            AlertCondition = "preview failures exceed target or any production preview returns catalog integrity errors",
            Escalation = "platform on-call reviews payload, contract errors, and source-mode health before submit"
        },
        new CatalogControlPlaneSloDefinition
        {
            Name = "catalog_submit_success",
            Target = $">= {_certificationOptions.SubmitSuccessSloPercent}% successful submits per rolling hour",
            AlertCondition = $"version conflicts >= {_certificationOptions.VersionConflictAlertThresholdPerHour}/hour or duplicate submits >= {_certificationOptions.DuplicateSubmitAlertThresholdPerHour}/hour",
            Escalation = "platform on-call checks idempotency keys, expected versions, and pending duplicate queue"
        },
        new CatalogControlPlaneSloDefinition
        {
            Name = "catalog_approve_success",
            Target = $">= {_certificationOptions.ApproveSuccessSloPercent}% successful approvals per rolling hour",
            AlertCondition = "high-risk approval denials or self-approval denials repeat in production-like environments",
            Escalation = "catalog governance owner reviews role assignment and two-person-control policy"
        },
        new CatalogControlPlaneSloDefinition
        {
            Name = "catalog_apply_success",
            Target = $">= {_certificationOptions.ApplySuccessSloPercent}% successful applies per rolling hour",
            AlertCondition = $"apply failures >= {_certificationOptions.ApplyFailureAlertThresholdPerHour}/hour",
            Escalation = "platform/database on-call verifies SQL availability, version drift, and replay safety"
        },
        new CatalogControlPlaneSloDefinition
        {
            Name = "catalog_rollback_readiness",
            Target = $"compensating rollback change can be previewed within {_certificationOptions.RollbackReadyMinutes} minutes",
            AlertCondition = $"rollback-linked changes >= {_certificationOptions.RollbackSurgeAlertThresholdPerHour}/hour",
            Escalation = "incident commander opens after-action review and validates rollback lineage"
        }
    };

    private async Task ValidateChangeForPromotionAsync(
        CatalogChangeRequestRecord change,
        bool productionLike,
        List<string> blockers,
        List<string> warnings,
        CancellationToken ct)
    {
        if (!string.Equals(change.Status, PlatformCatalogChangeStatus.Approved, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(change.Status, PlatformCatalogChangeStatus.Applied, StringComparison.OrdinalIgnoreCase))
        {
            blockers.Add("catalog_change_not_approved");
        }

        if (productionLike && string.IsNullOrWhiteSpace(change.ExpectedVersionTag))
        {
            var version = await _mutationStore.GetRecordVersionAsync(change.RecordType, change.RecordKey, ct);
            if (version.Exists && _controlPlaneOptions.RequireExpectedVersionForExistingRecordsInProductionLike)
            {
                blockers.Add("catalog_change_missing_expected_version");
            }
        }

        if (change.BreakGlass)
        {
            blockers.Add("catalog_break_glass_requires_after_action_evidence");
        }

        if (!string.IsNullOrWhiteSpace(change.RollbackOfChangeId))
        {
            warnings.Add($"catalog_rollback_lineage:{change.RollbackOfChangeId}");
        }
    }

    private string DetermineSourceMode()
    {
        var snapshot = _catalogProvider.Load();
        var bootstrapCapabilities = _configuration.GetSection("Capabilities").GetChildren().Count();
        var bootstrapConnections = _configuration.GetSection("ExternalConnections:Connections").GetChildren().Count();
        var platformCount = snapshot.Capabilities.Count + snapshot.ExternalConnections.Count;
        var bootstrapCount = bootstrapCapabilities + bootstrapConnections;

        if (!_catalogOptions.Enabled || platformCount == 0)
        {
            return bootstrapCount > 0 && _catalogOptions.AllowBootstrapConfigurationFallback
                ? "bootstrap_only"
                : "empty";
        }

        return bootstrapCount > 0 && _catalogOptions.AllowBootstrapConfigurationFallback
            ? "mixed"
            : "platform";
    }

    private bool IsProductionLike(string environment) =>
        _certificationOptions.ProductionLikeEnvironments.Any(item =>
            string.Equals(item, environment, StringComparison.OrdinalIgnoreCase));

    private string EffectiveEnvironmentName(string requestedEnvironment) =>
        !string.IsNullOrWhiteSpace(requestedEnvironment)
            ? requestedEnvironment.Trim()
            : !string.IsNullOrWhiteSpace(_certificationOptions.EnvironmentName)
                ? _certificationOptions.EnvironmentName.Trim()
                : "development";
}
