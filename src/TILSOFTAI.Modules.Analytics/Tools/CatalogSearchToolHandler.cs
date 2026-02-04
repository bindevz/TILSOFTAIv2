using Microsoft.Extensions.Logging;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Sql;
using TILSOFTAI.Orchestration.Tools;

namespace TILSOFTAI.Modules.Analytics.Tools;

/// <summary>
/// Handler for catalog_search tool - Schema RAG search.
/// </summary>
public sealed class CatalogSearchToolHandler : AnalyticsToolHandlerBase
{
    public override string ToolName => "catalog_search";

    public CatalogSearchToolHandler(
        ISqlExecutor sqlExecutor,
        IExecutionContextAccessor contextAccessor,
        ILogger<CatalogSearchToolHandler> logger)
        : base(sqlExecutor, contextAccessor, logger)
    {
    }

    public override async Task<string> ExecuteAsync(
        ToolDefinition tool,
        string argumentsJson,
        TilsoftExecutionContext context,
        CancellationToken ct)
    {
        var args = ParseArguments<CatalogSearchArgs>(argumentsJson);
        
        if (string.IsNullOrWhiteSpace(args?.Query))
        {
            throw new ArgumentException("Query is required for catalog_search");
        }

        var parameters = new Dictionary<string, object?>
        {
            ["TenantId"] = context.TenantId,
            ["Query"] = args.Query,
            ["TopK"] = args.TopK ?? 5,
            ["Domain"] = args.Domain ?? "internal"
        };

        return await ExecuteSpAsync("dbo.ai_catalog_search", parameters, ct);
    }

    private sealed class CatalogSearchArgs
    {
        public string? Query { get; set; }
        public int? TopK { get; set; }
        public string? Domain { get; set; }
    }
}
