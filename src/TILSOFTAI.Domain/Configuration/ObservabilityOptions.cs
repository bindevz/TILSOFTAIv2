namespace TILSOFTAI.Domain.Configuration;

public sealed class ObservabilityOptions
{
    public bool EnableSqlErrorLog { get; set; }
    public bool EnableSqlToolLog { get; set; }
    public bool EnableConversationPersistence { get; set; }
    public bool RedactLogs { get; set; } = true;
    public string RedactionMode { get; set; } = "basic";
    public int RetentionDays { get; set; } = 30;
}
