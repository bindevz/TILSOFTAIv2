using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Orchestration.Capabilities;

namespace TILSOFTAI.Infrastructure.Catalog;

public interface IPlatformCatalogControlPlane
{
    Task<IReadOnlyList<CapabilityDescriptor>> ListCapabilitiesAsync(CatalogMutationContext context, CancellationToken ct);
    Task<IReadOnlyList<KeyValuePair<string, ExternalConnectionOptions>>> ListExternalConnectionsAsync(CatalogMutationContext context, CancellationToken ct);
    Task<IReadOnlyList<CatalogChangeRequestRecord>> ListChangesAsync(CatalogMutationContext context, CancellationToken ct);
    Task<CatalogChangeRequestRecord> ProposeAsync(CatalogMutationRequest request, CatalogMutationContext context, CancellationToken ct);
    Task<CatalogChangeRequestRecord> ApproveAsync(string changeId, CatalogMutationContext context, CancellationToken ct);
    Task<CatalogChangeRequestRecord> RejectAsync(string changeId, CatalogMutationContext context, CancellationToken ct);
    Task<CatalogChangeRequestRecord> ApplyAsync(string changeId, CatalogMutationContext context, CancellationToken ct);
}
