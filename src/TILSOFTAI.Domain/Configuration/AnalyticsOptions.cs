namespace TILSOFTAI.Domain.Configuration;

/// <summary>
/// Configuration options for the Analytics workflow.
/// </summary>
public sealed class AnalyticsOptions
{
    /// <summary>
    /// Maximum number of rows returned by analytics queries.
    /// </summary>
    public int MaxRows { get; set; } = 200;

    /// <summary>
    /// Maximum number of groupBy fields allowed.
    /// </summary>
    public int MaxGroupBy { get; set; } = 4;

    /// <summary>
    /// Maximum number of metrics allowed per query.
    /// </summary>
    public int MaxMetrics { get; set; } = 3;

    /// <summary>
    /// Maximum number of joins allowed (start with 1 hop).
    /// </summary>
    public int MaxJoins { get; set; } = 1;

    /// <summary>
    /// Maximum time window in days for time-range queries.
    /// </summary>
    public int MaxTimeWindowDays { get; set; } = 366;

    /// <summary>
    /// Allowed metric operations.
    /// </summary>
    public string[] AllowedMetricOps { get; set; } = 
        { "count", "countDistinct", "sum", "avg", "min", "max" };

    /// <summary>
    /// Maximum plan validation retries.
    /// </summary>
    public int MaxPlanRetries { get; set; } = 2;

    /// <summary>
    /// Enable task frame persistence for audit.
    /// </summary>
    public bool EnableTaskFramePersistence { get; set; } = true;

    /// <summary>
    /// Enable insight caching.
    /// </summary>
    public bool EnableInsightCache { get; set; } = true;

    /// <summary>
    /// Default insight cache TTL in seconds.
    /// </summary>
    public int InsightCacheTtlSeconds { get; set; } = 300;

    /// <summary>
    /// Enable deterministic analytics orchestration.
    /// When enabled, analytics queries bypass free-form LLM flow.
    /// PATCH 29.02
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum tool calls per analytics turn.
    /// PATCH 29.02
    /// </summary>
    public int MaxToolCallsPerTurn { get; set; } = 10;

    /// <summary>
    /// Default TopN limit for breakdowns.
    /// PATCH 29.02
    /// </summary>
    public int DefaultTopN { get; set; } = 10;

    /// <summary>
    /// Maximum breakdown tables to include in output.
    /// PATCH 29.02
    /// </summary>
    public int MaxBreakdownTables { get; set; } = 2;
}

