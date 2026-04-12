using Microsoft.AspNetCore.Mvc;
using TILSOFTAI.Api.Contracts.Catalog;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Infrastructure.Catalog;
using TILSOFTAI.Orchestration.Capabilities;

namespace TILSOFTAI.Api.Controllers;

[ApiController]
[Route("api/platform-catalog")]
public sealed class PlatformCatalogController : ControllerBase
{
    private readonly IPlatformCatalogControlPlane _controlPlane;
    private readonly IExecutionContextAccessor _contextAccessor;

    public PlatformCatalogController(
        IPlatformCatalogControlPlane controlPlane,
        IExecutionContextAccessor contextAccessor)
    {
        _controlPlane = controlPlane ?? throw new ArgumentNullException(nameof(controlPlane));
        _contextAccessor = contextAccessor ?? throw new ArgumentNullException(nameof(contextAccessor));
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
}
