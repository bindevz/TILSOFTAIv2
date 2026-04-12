using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Orchestration.Capabilities;

namespace TILSOFTAI.Infrastructure.Catalog;

public static class PlatformCatalogRecordTypes
{
    public const string Capability = "capability";
    public const string ExternalConnection = "external_connection";
}

public static class PlatformCatalogOperations
{
    public const string Upsert = "upsert";
    public const string Disable = "disable";
}

public static class PlatformCatalogChangeStatus
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Applied = "Applied";
}

public sealed class CatalogMutationContext
{
    public string TenantId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
    public string CorrelationId { get; init; } = string.Empty;
}

public sealed class CatalogMutationRequest
{
    public string RecordType { get; init; } = string.Empty;
    public string Operation { get; init; } = PlatformCatalogOperations.Upsert;
    public string RecordKey { get; init; } = string.Empty;
    public CapabilityDescriptor? Capability { get; init; }
    public ExternalConnectionOptions? ExternalConnection { get; init; }
    public string Owner { get; init; } = string.Empty;
    public string ChangeNote { get; init; } = string.Empty;
    public string VersionTag { get; init; } = string.Empty;
    public string ExpectedVersionTag { get; init; } = string.Empty;
    public string IdempotencyKey { get; init; } = string.Empty;
    public string RollbackOfChangeId { get; init; } = string.Empty;
    public bool BreakGlass { get; init; }
    public string BreakGlassJustification { get; init; } = string.Empty;
}

public sealed class CatalogChangeRequestRecord
{
    public string ChangeId { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public string RecordType { get; init; } = string.Empty;
    public string Operation { get; init; } = string.Empty;
    public string RecordKey { get; init; } = string.Empty;
    public string PayloadJson { get; init; } = "{}";
    public string Status { get; init; } = PlatformCatalogChangeStatus.Pending;
    public string Owner { get; init; } = string.Empty;
    public string ChangeNote { get; init; } = string.Empty;
    public string VersionTag { get; init; } = string.Empty;
    public string ExpectedVersionTag { get; init; } = string.Empty;
    public string IdempotencyKey { get; init; } = string.Empty;
    public string RollbackOfChangeId { get; init; } = string.Empty;
    public string PayloadHash { get; init; } = string.Empty;
    public string RiskLevel { get; init; } = CatalogChangeRiskLevels.Standard;
    public string EnvironmentName { get; init; } = string.Empty;
    public bool BreakGlass { get; init; }
    public string BreakGlassJustification { get; init; } = string.Empty;
    public string RequestedByUserId { get; init; } = string.Empty;
    public DateTime RequestedAtUtc { get; init; } = DateTime.UtcNow;
    public string? ReviewedByUserId { get; init; }
    public DateTime? ReviewedAtUtc { get; init; }
    public string? AppliedByUserId { get; init; }
    public DateTime? AppliedAtUtc { get; init; }
}

public static class CatalogChangeRiskLevels
{
    public const string Standard = "standard";
    public const string High = "high";
}

public sealed class CatalogRecordVersion
{
    public bool Exists { get; init; }
    public string VersionTag { get; init; } = string.Empty;
}

public sealed class CatalogMutationPreviewResult
{
    public bool IsValid { get; init; }
    public string RecordType { get; init; } = string.Empty;
    public string Operation { get; init; } = string.Empty;
    public string RecordKey { get; init; } = string.Empty;
    public string RiskLevel { get; init; } = CatalogChangeRiskLevels.Standard;
    public string EnvironmentName { get; init; } = string.Empty;
    public bool ExistingRecordFound { get; init; }
    public string CurrentVersionTag { get; init; } = string.Empty;
    public string ExpectedVersionTag { get; init; } = string.Empty;
    public string PayloadHash { get; init; } = string.Empty;
    public string? DuplicatePendingChangeId { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
