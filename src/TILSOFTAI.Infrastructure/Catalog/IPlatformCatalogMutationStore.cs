using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Orchestration.Capabilities;

namespace TILSOFTAI.Infrastructure.Catalog;

public interface IPlatformCatalogMutationStore
{
    Task<IReadOnlyList<CapabilityDescriptor>> ListCapabilitiesAsync(CancellationToken ct);
    Task<IReadOnlyList<KeyValuePair<string, ExternalConnectionOptions>>> ListExternalConnectionsAsync(CancellationToken ct);
    Task<IReadOnlyList<CatalogChangeRequestRecord>> ListChangesAsync(string tenantId, CancellationToken ct);
    Task<CatalogChangeRequestRecord?> GetChangeAsync(string tenantId, string changeId, CancellationToken ct);
    Task<CatalogChangeRequestRecord> CreateChangeAsync(CatalogChangeRequestRecord change, CancellationToken ct);
    Task<CatalogChangeRequestRecord> ApproveChangeAsync(string tenantId, string changeId, string reviewerUserId, CancellationToken ct);
    Task<CatalogChangeRequestRecord> RejectChangeAsync(string tenantId, string changeId, string reviewerUserId, CancellationToken ct);
    Task<CatalogChangeRequestRecord> MarkAppliedAsync(string tenantId, string changeId, string appliedByUserId, CancellationToken ct);
    Task UpsertCapabilityAsync(CapabilityDescriptor capability, CatalogChangeRequestRecord change, CancellationToken ct);
    Task DisableCapabilityAsync(string capabilityKey, CatalogChangeRequestRecord change, CancellationToken ct);
    Task UpsertExternalConnectionAsync(string connectionName, ExternalConnectionOptions connection, CatalogChangeRequestRecord change, CancellationToken ct);
    Task DisableExternalConnectionAsync(string connectionName, CatalogChangeRequestRecord change, CancellationToken ct);
}
