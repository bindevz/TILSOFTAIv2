namespace TILSOFTAI.Domain.Configuration;

/// <summary>
/// Centralized default values for all configuration options.
/// These values are used when configuration is not explicitly set.
/// </summary>
public static class ConfigurationDefaults
{
    /// <summary>
    /// Chat pipeline configuration defaults.
    /// </summary>
    public static class Chat
    {
        /// <summary>Maximum input characters (32,000).</summary>
        public const int MaxInputChars = 32_000;

        /// <summary>Maximum messages in conversation context (50).</summary>
        public const int MaxMessages = 50;

        /// <summary>Maximum LLM-tool loop iterations (10).</summary>
        public const int MaxSteps = 10;

        /// <summary>Maximum tool calls per request (20).</summary>
        public const int MaxToolCallsPerRequest = 20;

        /// <summary>Maximum request body size in bytes (256KB).</summary>
        public const long MaxRequestBytes = 256 * 1024;

        /// <summary>Maximum tool result size before compaction (16KB).</summary>
        public const int ToolResultMaxBytes = 16_000;
    }

    /// <summary>
    /// SQL Server configuration defaults.
    /// </summary>
    public static class Sql
    {
        /// <summary>Command timeout in seconds (30).</summary>
        public const int CommandTimeoutSeconds = 30;

        /// <summary>Minimum connection pool size (5).</summary>
        public const int MinPoolSize = 5;

        /// <summary>Maximum connection pool size (100).</summary>
        public const int MaxPoolSize = 100;

        /// <summary>Connection timeout in seconds (15).</summary>
        public const int ConnectionTimeoutSeconds = 15;

        /// <summary>Required prefix for model-callable stored procedures.</summary>
        public const string ModelCallableSpPrefix = "ai_";

        /// <summary>Prefix for internal stored procedures.</summary>
        public const string InternalSpPrefix = "app_";

        /// <summary>Default application name for SQL connections.</summary>
        public const string ApplicationName = "TILSOFTAI";
    }

    /// <summary>
    /// Redis caching configuration defaults.
    /// </summary>
    public static class Redis
    {
        /// <summary>Default cache TTL in minutes (30). Minimum allowed value.</summary>
        public const int DefaultTtlMinutes = 30;

        /// <summary>Minimum allowed TTL in minutes (30).</summary>
        public const int MinTtlMinutes = 30;
    }

    /// <summary>
    /// LLM client configuration defaults.
    /// </summary>
    public static class Llm
    {
        /// <summary>Default temperature for LLM requests (0.7).</summary>
        public const double Temperature = 0.7;

        /// <summary>Maximum response tokens (4096).</summary>
        public const int MaxResponseTokens = 4096;

        /// <summary>Request timeout in seconds (60).</summary>
        public const int TimeoutSeconds = 60;

        /// <summary>Default LLM provider when not configured.</summary>
        public const string DefaultProvider = "Null";
    }

    /// <summary>
    /// Input validation configuration defaults.
    /// </summary>
    public static class Validation
    {
        /// <summary>Maximum input length in characters (32,000).</summary>
        public const int MaxInputLength = 32_000;

        /// <summary>Maximum tool argument JSON length (64KB).</summary>
        public const int MaxToolArgumentLength = 65_536;
    }

    /// <summary>
    /// Streaming configuration defaults.
    /// </summary>
    public static class Streaming
    {
        /// <summary>Channel buffer capacity (100 events).</summary>
        public const int ChannelCapacity = 100;

        /// <summary>Whether to drop delta events when buffer is full.</summary>
        public const bool DropDeltaWhenFull = true;
    }

    /// <summary>
    /// Rate limiting configuration defaults.
    /// </summary>
    public static class RateLimit
    {
        /// <summary>Requests permitted per window (100).</summary>
        public const int PermitLimit = 100;

        /// <summary>Rate limit window in seconds (60).</summary>
        public const int WindowSeconds = 60;

        /// <summary>Queue limit for excess requests (0 = no queue).</summary>
        public const int QueueLimit = 0;
    }

    /// <summary>
    /// Resilience (circuit breaker, retry) configuration defaults.
    /// </summary>
    public static class Resilience
    {
        /// <summary>Failures before circuit opens (5).</summary>
        public const int CircuitBreakerFailureThreshold = 5;

        /// <summary>Circuit open duration in seconds (30).</summary>
        public const int CircuitBreakerDurationSeconds = 30;

        /// <summary>Maximum retry attempts (3).</summary>
        public const int RetryCount = 3;

        /// <summary>Base retry delay in milliseconds (200).</summary>
        public const int RetryDelayMs = 200;
    }

    /// <summary>
    /// Audit logging configuration defaults.
    /// </summary>
    public static class Audit
    {
        /// <summary>Audit log retention in days (90).</summary>
        public const int RetentionDays = 90;

        /// <summary>In-memory buffer size for async processing (1000).</summary>
        public const int BufferSize = 1000;
    }

    /// <summary>
    /// Observability data retention defaults.
    /// </summary>
    public static class Observability
    {
        /// <summary>Conversation/tool execution retention in days (30).</summary>
        public const int PurgeRetentionDays = 30;
    }

    /// <summary>
    /// Localization configuration defaults.
    /// </summary>
    public static class Localization
    {
        /// <summary>Default language when not specified.</summary>
        public const string DefaultLanguage = "en";

        /// <summary>Default supported languages.</summary>
        public static readonly string[] SupportedLanguages = { "en", "vi" };
    }
}
