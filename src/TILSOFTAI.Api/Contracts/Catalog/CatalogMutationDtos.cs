using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Infrastructure.Catalog;
using TILSOFTAI.Orchestration.Capabilities;

namespace TILSOFTAI.Api.Contracts.Catalog;

public sealed class CatalogMutationApiRequest
{
    public string RecordType { get; init; } = string.Empty;
    public string Operation { get; init; } = PlatformCatalogOperations.Upsert;
    public string RecordKey { get; init; } = string.Empty;
    public CapabilityDescriptor? Capability { get; init; }
    public ExternalConnectionOptions? ExternalConnection { get; init; }
    public string Owner { get; init; } = string.Empty;
    public string ChangeNote { get; init; } = string.Empty;
    public string VersionTag { get; init; } = string.Empty;

    public CatalogMutationRequest ToMutationRequest() => new()
    {
        RecordType = RecordType,
        Operation = Operation,
        RecordKey = RecordKey,
        Capability = Capability,
        ExternalConnection = ExternalConnection,
        Owner = Owner,
        ChangeNote = ChangeNote,
        VersionTag = VersionTag
    };
}
