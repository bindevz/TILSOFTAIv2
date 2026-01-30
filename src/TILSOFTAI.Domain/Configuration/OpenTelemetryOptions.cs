namespace TILSOFTAI.Domain.Configuration;

/// <summary>
/// Configuration for OpenTelemetry distributed tracing.
/// Enables W3C trace context propagation across HTTP, SQL, and external services.
/// </summary>
public sealed class OpenTelemetryOptions
{
    /// <summary>
    /// Enable OpenTelemetry tracing.
    /// Default: false (opt-in to avoid breaking existing deployments).
    /// </summary>
    public bool Enabled { get; set; } = false;
    
    /// <summary>
    /// Service name for telemetry (appears in trace viewers).
    /// Default: "TILSOFTAI".
    /// </summary>
    public string ServiceName { get; set; } = "TILSOFTAI";
    
    /// <summary>
    /// Service version for telemetry.
    /// Default: "1.0.0".
    /// </summary>
    public string ServiceVersion { get; set; } = "1.0.0";
    
    /// <summary>
    /// Exporter type: "console" (development), "otlp" (production), or "none".
    /// Default: "console".
    /// </summary>
    public string ExporterType { get; set; } = "console";
    
    /// <summary>
    /// OTLP endpoint for production telemetry (if ExporterType is "otlp").
    /// Example: "http://localhost:4317" or "http://jaeger-collector:4317".
    /// </summary>
    public string? OtlpEndpoint { get; set; }

    /// <summary>
    /// Emit auth/JWKS refresh activities for diagnostics.
    /// Default: false.
    /// </summary>
    public bool EnableAuthKeyRefreshTracing { get; set; } = false;
}
