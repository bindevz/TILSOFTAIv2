using TILSOFTAI.Domain.Analytics;
using TILSOFTAI.Domain.ExecutionContext;

namespace TILSOFTAI.Orchestration.Analytics;

/// <summary>
/// Service to assemble query results into structured insight output.
/// PATCH 30.05: Added validatedPlanJson for accurate notes from executed plan.
/// </summary>
public interface IInsightAssemblyService
{
    /// <summary>
    /// Assemble insight from query results.
    /// PATCH 30.05: Added validatedPlanJson for notes fidelity.
    /// </summary>
    Task<InsightOutput> AssembleAsync(
        TaskFrame taskFrame,
        IReadOnlyList<QueryResultSet> queryResults,
        string? validatedPlanJson,
        TilsoftExecutionContext context,
        CancellationToken ct);
}

/// <summary>
/// Represents a query result set with metadata.
/// </summary>
public sealed class QueryResultSet
{
    public string Label { get; set; } = string.Empty;
    public QueryResultType Type { get; set; }
    public List<string> Columns { get; set; } = new();
    public List<List<object?>> Rows { get; set; } = new();
    public int RowCount { get; set; }
    public bool Truncated { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public enum QueryResultType
{
    Total,
    Breakdown,
    Drilldown
}
