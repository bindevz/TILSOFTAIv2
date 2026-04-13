using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TILSOFTAI.Api.Contracts.Catalog;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Domain.Metrics;
using TILSOFTAI.Infrastructure.Catalog;
using TILSOFTAI.Orchestration.Capabilities;

namespace TILSOFTAI.Api.Controllers;

[ApiController]
[Route("api/platform-catalog")]
public sealed class PlatformCatalogController : ControllerBase
{
    private readonly IPlatformCatalogControlPlane _controlPlane;
    private readonly IPlatformCatalogPromotionGate _promotionGate;
    private readonly IPlatformCatalogCertificationStore _certificationStore;
    private readonly IPlatformCatalogEvidenceVerifier _evidenceVerifier;
    private readonly IPlatformCatalogPromotionManifestStore _manifestStore;
    private readonly IPlatformCatalogPromotionManifestService _manifestService;
    private readonly IExecutionContextAccessor _contextAccessor;
    private readonly CatalogControlPlaneOptions _options;
    private readonly IMetricsService _metrics;

    public PlatformCatalogController(
        IPlatformCatalogControlPlane controlPlane,
        IPlatformCatalogPromotionGate promotionGate,
        IPlatformCatalogCertificationStore certificationStore,
        IPlatformCatalogEvidenceVerifier evidenceVerifier,
        IPlatformCatalogPromotionManifestStore manifestStore,
        IPlatformCatalogPromotionManifestService manifestService,
        IExecutionContextAccessor contextAccessor,
        IOptions<CatalogControlPlaneOptions> options,
        IMetricsService metrics)
    {
        _controlPlane = controlPlane ?? throw new ArgumentNullException(nameof(controlPlane));
        _promotionGate = promotionGate ?? throw new ArgumentNullException(nameof(promotionGate));
        _certificationStore = certificationStore ?? throw new ArgumentNullException(nameof(certificationStore));
        _evidenceVerifier = evidenceVerifier ?? throw new ArgumentNullException(nameof(evidenceVerifier));
        _manifestStore = manifestStore ?? throw new ArgumentNullException(nameof(manifestStore));
        _manifestService = manifestService ?? throw new ArgumentNullException(nameof(manifestService));
        _contextAccessor = contextAccessor ?? throw new ArgumentNullException(nameof(contextAccessor));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
    }

    [HttpGet("capabilities")]
    public async Task<ActionResult<IReadOnlyList<CapabilityDescriptor>>> ListCapabilities(CancellationToken ct)
    {
        var records = await _controlPlane.ListCapabilitiesAsync(ToCatalogContext(), ct);
        return Ok(records);
    }

    [HttpGet("external-connections")]
    public async Task<ActionResult<IReadOnlyList<object>>> ListExternalConnections(CancellationToken ct)
    {
        var records = await _controlPlane.ListExternalConnectionsAsync(ToCatalogContext(), ct);
        return Ok(records.Select(pair => new { connectionName = pair.Key, connection = pair.Value }).ToArray());
    }

    [HttpGet("changes")]
    public async Task<ActionResult<IReadOnlyList<CatalogChangeRequestRecord>>> ListChanges(CancellationToken ct)
    {
        var records = await _controlPlane.ListChangesAsync(ToCatalogContext(), ct);
        return Ok(records);
    }

    [HttpPost("changes")]
    public async Task<ActionResult<CatalogChangeRequestRecord>> Propose(
        [FromBody] CatalogMutationApiRequest request,
        CancellationToken ct)
    {
        var record = await _controlPlane.ProposeAsync(request.ToMutationRequest(), ToCatalogContext(), ct);
        return AcceptedAtAction(nameof(ListChanges), new { id = record.ChangeId }, record);
    }

    [HttpPost("changes/preview")]
    public async Task<ActionResult<CatalogMutationPreviewResult>> Preview(
        [FromBody] CatalogMutationApiRequest request,
        CancellationToken ct)
    {
        var result = await _controlPlane.PreviewAsync(request.ToMutationRequest(), ToCatalogContext(), ct);
        return Ok(result);
    }

    [HttpPost("promotion-gate/evaluate")]
    public async Task<ActionResult<CatalogPromotionGateResult>> EvaluatePromotionGate(
        [FromBody] CatalogPromotionGateApiRequest? request,
        CancellationToken ct)
    {
        if (request is null)
        {
            return BadRequest("promotion gate request is required.");
        }

        var context = ToCatalogContext();
        RequireAnyCatalogRole(context);
        var result = await _promotionGate.EvaluateAsync(request.ToGateRequest(), context, ct);
        return Ok(result);
    }

    [HttpGet("slo-definitions")]
    public ActionResult<IReadOnlyList<CatalogControlPlaneSloDefinition>> GetSloDefinitions()
    {
        return Ok(_promotionGate.GetSloDefinitions());
    }

    [HttpGet("certification-evidence")]
    public async Task<ActionResult<IReadOnlyList<CatalogCertificationEvidenceRecord>>> ListCertificationEvidence(
        [FromQuery] string environmentName,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(environmentName))
        {
            return BadRequest("environmentName is required.");
        }

        RequireAnyCatalogRole(ToCatalogContext());
        var records = await _certificationStore.ListEvidenceAsync(environmentName.Trim(), ct);
        return Ok(records);
    }

    [HttpPost("certification-evidence")]
    public async Task<ActionResult<CatalogCertificationEvidenceRecord>> CreateCertificationEvidence(
        [FromBody] CatalogCertificationEvidenceApiRequest? request,
        CancellationToken ct)
    {
        if (request is null)
        {
            return BadRequest("certification evidence request is required.");
        }

        var validationError = ValidateCertificationEvidenceRequest(request);
        if (validationError is not null)
        {
            return validationError;
        }

        var context = ToCatalogContext();
        RequireAnyCatalogRole(context);
        var record = new CatalogCertificationEvidenceRecord
        {
            EvidenceId = Guid.NewGuid().ToString("N"),
            EnvironmentName = request.EnvironmentName.Trim(),
            EvidenceKind = request.EvidenceKind.Trim(),
            Status = request.Status.Trim().ToLowerInvariant(),
            Summary = request.Summary.Trim(),
            EvidenceUri = request.EvidenceUri?.Trim() ?? string.Empty,
            RelatedChangeId = request.RelatedChangeId?.Trim() ?? string.Empty,
            RelatedIncidentId = request.RelatedIncidentId?.Trim() ?? string.Empty,
            OperatorUserId = context.UserId,
            ApprovedByUserId = request.ApprovedByUserId?.Trim() ?? string.Empty,
            CorrelationId = context.CorrelationId,
            CapturedAtUtc = DateTime.UtcNow,
            ArtifactHash = request.ArtifactHash?.Trim() ?? string.Empty,
            ArtifactHashAlgorithm = request.ArtifactHashAlgorithm?.Trim() ?? "sha256",
            ArtifactContentType = request.ArtifactContentType?.Trim() ?? string.Empty,
            ArtifactType = request.ArtifactType?.Trim() ?? string.Empty,
            SourceSystem = request.SourceSystem?.Trim() ?? string.Empty,
            CollectedAtUtc = request.CollectedAtUtc,
            SignedPayload = request.SignedPayload?.Trim() ?? string.Empty,
            Signature = request.Signature?.Trim() ?? string.Empty,
            SignatureAlgorithm = request.SignatureAlgorithm?.Trim() ?? string.Empty,
            SignerId = request.SignerId?.Trim() ?? string.Empty,
            SignerPublicKeyId = request.SignerPublicKeyId?.Trim() ?? string.Empty,
            VerificationStatus = CatalogEvidenceVerificationStatus.Unverified
        };

        var created = await _certificationStore.CreateEvidenceAsync(record, ct);
        _metrics.IncrementCounter(MetricNames.PlatformCatalogCertificationEvidenceTotal, new Dictionary<string, string>
        {
            ["environment"] = created.EnvironmentName,
            ["evidence_kind"] = created.EvidenceKind,
            ["status"] = created.Status
        });
        return CreatedAtAction(nameof(ListCertificationEvidence), new { environmentName = created.EnvironmentName }, created);
    }

    [HttpPost("certification-evidence/{evidenceId}/verify")]
    public async Task<ActionResult<CatalogCertificationEvidenceRecord>> VerifyCertificationEvidence(
        string evidenceId,
        [FromBody] CatalogCertificationEvidenceVerifyApiRequest? request,
        CancellationToken ct)
    {
        if (request is null)
        {
            return BadRequest("certification evidence verification request is required.");
        }

        var context = ToCatalogContext();
        RequireAnyCatalogRole(context);
        var evidence = await _certificationStore.GetEvidenceAsync(evidenceId, ct);
        if (evidence is null)
        {
            return NotFound();
        }

        var result = _evidenceVerifier.Verify(evidence, context, request.AcceptAsTrusted, request.VerificationNotes);
        var updated = await _certificationStore.UpdateEvidenceVerificationAsync(evidenceId, result, ct);
        _metrics.IncrementCounter(MetricNames.PlatformCatalogCertificationEvidenceTotal, new Dictionary<string, string>
        {
            ["environment"] = updated.EnvironmentName,
            ["evidence_kind"] = updated.EvidenceKind,
            ["status"] = updated.Status
        });

        return Ok(updated);
    }

    [HttpGet("promotion-manifests")]
    public async Task<ActionResult<IReadOnlyList<CatalogPromotionManifestRecord>>> ListPromotionManifests(
        [FromQuery] string environmentName,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(environmentName))
        {
            return BadRequest("environmentName is required.");
        }

        RequireAnyCatalogRole(ToCatalogContext());
        var records = await _manifestStore.ListManifestsAsync(environmentName.Trim(), ct);
        return Ok(records);
    }

    [HttpGet("promotion-manifests/{manifestId}")]
    public async Task<ActionResult<CatalogPromotionManifestRecord>> GetPromotionManifest(
        string manifestId,
        CancellationToken ct)
    {
        RequireAnyCatalogRole(ToCatalogContext());
        var record = await _manifestStore.GetManifestAsync(manifestId, ct);
        return record is null ? NotFound() : Ok(record);
    }

    [HttpPost("promotion-manifests")]
    public async Task<ActionResult<CatalogPromotionManifestIssueResult>> IssuePromotionManifest(
        [FromBody] CatalogPromotionManifestIssueApiRequest? request,
        CancellationToken ct)
    {
        if (request is null)
        {
            return BadRequest("promotion manifest issue request is required.");
        }

        var context = ToCatalogContext();
        RequireAnyCatalogRole(context);
        var result = await _manifestService.IssueManifestAsync(request.ToIssueRequest(), context, ct);
        if (!result.IsIssued || result.Manifest is null)
        {
            return BadRequest(result);
        }

        return CreatedAtAction(nameof(GetPromotionManifest), new { manifestId = result.Manifest.ManifestId }, result);
    }

    [HttpPost("promotion-manifests/{manifestId}/attestations")]
    public async Task<ActionResult<CatalogRolloutAttestationResult>> RecordPromotionAttestation(
        string manifestId,
        [FromBody] CatalogRolloutAttestationApiRequest? request,
        CancellationToken ct)
    {
        if (request is null)
        {
            return BadRequest("rollout attestation request is required.");
        }

        var context = ToCatalogContext();
        RequireAnyCatalogRole(context);
        var result = await _manifestService.RecordAttestationAsync(manifestId, request.ToAttestationRequest(), context, ct);
        return result.IsRecorded ? Ok(result) : BadRequest(result);
    }

    [HttpGet("promotion-manifests/{manifestId}/dossier")]
    public async Task<ActionResult<CatalogPromotionDossier>> GetPromotionDossier(
        string manifestId,
        CancellationToken ct)
    {
        var context = ToCatalogContext();
        RequireAnyCatalogRole(context);
        var dossier = await _manifestService.GetDossierAsync(manifestId, context, ct);
        return dossier is null ? NotFound() : Ok(dossier);
    }

    [HttpPost("promotion-manifests/{manifestId}/dossier/archive")]
    public async Task<ActionResult<CatalogDossierArchiveResult>> ArchivePromotionDossier(
        string manifestId,
        CancellationToken ct)
    {
        var context = ToCatalogContext();
        RequireAnyCatalogRole(context);
        var result = await _manifestService.ArchiveDossierAsync(manifestId, context, ct);
        return result.IsArchived ? Ok(result) : BadRequest(result);
    }

    [HttpPost("changes/{changeId}/approve")]
    public async Task<ActionResult<CatalogChangeRequestRecord>> Approve(string changeId, CancellationToken ct)
    {
        var record = await _controlPlane.ApproveAsync(changeId, ToCatalogContext(), ct);
        return Ok(record);
    }

    [HttpPost("changes/{changeId}/reject")]
    public async Task<ActionResult<CatalogChangeRequestRecord>> Reject(string changeId, CancellationToken ct)
    {
        var record = await _controlPlane.RejectAsync(changeId, ToCatalogContext(), ct);
        return Ok(record);
    }

    [HttpPost("changes/{changeId}/apply")]
    public async Task<ActionResult<CatalogChangeRequestRecord>> Apply(string changeId, CancellationToken ct)
    {
        var record = await _controlPlane.ApplyAsync(changeId, ToCatalogContext(), ct);
        return Ok(record);
    }

    private CatalogMutationContext ToCatalogContext()
    {
        var context = _contextAccessor.Current;
        return new CatalogMutationContext
        {
            TenantId = context.TenantId,
            UserId = context.UserId,
            Roles = context.Roles,
            CorrelationId = context.CorrelationId
        };
    }

    private void RequireAnyCatalogRole(CatalogMutationContext context)
    {
        var allowedRoles = _options.SubmitRoles
            .Concat(_options.ApproveRoles)
            .Concat(_options.ApplyRoles)
            .Concat(_options.HighRiskApproveRoles)
            .Concat(_options.BreakGlassRoles)
            .ToArray();

        if (allowedRoles.Length == 0
            || allowedRoles.Any(role => context.Roles.Contains(role, StringComparer.OrdinalIgnoreCase)))
        {
            return;
        }

        throw new UnauthorizedAccessException("User does not have a catalog control-plane role.");
    }

    private ActionResult? ValidateCertificationEvidenceRequest(CatalogCertificationEvidenceApiRequest? request)
    {
        if (request is null)
        {
            return BadRequest("certification evidence request is required.");
        }

        if (string.IsNullOrWhiteSpace(request.EnvironmentName))
        {
            return BadRequest("environmentName is required.");
        }

        if (string.IsNullOrWhiteSpace(request.EvidenceKind))
        {
            return BadRequest("evidenceKind is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Status))
        {
            return BadRequest("status is required.");
        }

        if (!IsKnownEvidenceStatus(request.Status))
        {
            return BadRequest("status must be recorded, verified, accepted, expired, superseded, pending, or rejected.");
        }

        if (string.IsNullOrWhiteSpace(request.Summary))
        {
            return BadRequest("summary is required.");
        }

        return null;
    }

    private static bool IsKnownEvidenceStatus(string status) =>
        string.Equals(status, CatalogCertificationEvidenceStatus.Accepted, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, CatalogCertificationEvidenceStatus.Recorded, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, CatalogCertificationEvidenceStatus.Verified, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, CatalogCertificationEvidenceStatus.Expired, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, CatalogCertificationEvidenceStatus.Superseded, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, CatalogCertificationEvidenceStatus.Pending, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, CatalogCertificationEvidenceStatus.Rejected, StringComparison.OrdinalIgnoreCase);
}
