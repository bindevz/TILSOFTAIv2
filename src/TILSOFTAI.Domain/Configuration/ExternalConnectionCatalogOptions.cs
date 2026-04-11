namespace TILSOFTAI.Domain.Configuration;

public sealed class ExternalConnectionCatalogOptions
{
    public Dictionary<string, ExternalConnectionOptions> Connections { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ExternalConnectionOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string AuthScheme { get; set; } = string.Empty;
    public string AuthTokenSecret { get; set; } = string.Empty;
    public string ApiKeyHeader { get; set; } = string.Empty;
    public string ApiKeySecret { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; }
    public int RetryCount { get; set; }
    public int RetryDelayMs { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> HeaderSecrets { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
