namespace TILSOFTAI.Domain.Configuration;

public enum SensitiveHandlingMode
{
    Redact,
    MetadataOnly,
    DisablePersistence
}

public sealed class SensitiveDataOptions
{
    public SensitiveHandlingMode HandlingMode { get; set; } = SensitiveHandlingMode.Redact;
    public bool DisableCachingWhenSensitive { get; set; } = true;
    public bool DisableToolResultPersistenceWhenSensitive { get; set; } = true;
}
