namespace TILSOFTAI.Domain.Configuration
{
    public class LoggingOptions
    {
        public bool StructuredLoggingEnabled { get; set; } = true;
        public LogOutputFormat OutputFormat { get; set; } = LogOutputFormat.Json;
        public bool IncludeScopes { get; set; } = true;
        public string[] RedactedFields { get; set; } = new[] 
        { 
            "password", 
            "token", 
            "apikey", 
            "secret", 
            "connectionstring",
            "authorization",
            "cookie"
        };
        public int MaxPropertyValueLength { get; set; } = 1000;
        public bool EnableRequestResponseLogging { get; set; } = false;
        public double SamplingRate { get; set; } = 1.0;
    }

    public enum LogOutputFormat
    {
        Json,
        Console
    }
}
