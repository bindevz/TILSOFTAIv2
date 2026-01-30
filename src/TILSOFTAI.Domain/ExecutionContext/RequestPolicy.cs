using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Domain.ExecutionContext;

public sealed class RequestPolicy
{
    public static readonly RequestPolicy Default = new();

    public bool ContainsSensitive { get; init; }
    public SensitiveHandlingMode HandlingMode { get; init; } = SensitiveHandlingMode.Redact;
    public bool DisableCachingWhenSensitive { get; init; } = true;
    public bool DisableToolResultPersistenceWhenSensitive { get; init; } = true;

    public bool ShouldBypassCache => ContainsSensitive && DisableCachingWhenSensitive;
    public bool ShouldDisableToolResultPersistence => ContainsSensitive && DisableToolResultPersistenceWhenSensitive;
    public bool ShouldRedact => ContainsSensitive && HandlingMode == SensitiveHandlingMode.Redact;
    public bool IsMetadataOnly => ContainsSensitive && HandlingMode == SensitiveHandlingMode.MetadataOnly;
    public bool DisablePersistence => ContainsSensitive && HandlingMode == SensitiveHandlingMode.DisablePersistence;
}
