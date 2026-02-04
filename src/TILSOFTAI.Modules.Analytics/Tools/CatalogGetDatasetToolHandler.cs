using Microsoft.Extensions.Logging;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Sql;
using TILSOFTAI.Orchestration.Tools;

namespace TILSOFTAI.Modules.Analytics.Tools;

/// <summary>
/// Handler for catalog_get_dataset tool - Get full dataset schema.
/// </summary>
public sealed class CatalogGetDatasetToolHandler : AnalyticsToolHandlerBase
{
    public override string ToolName => "catalog_get_dataset";

    public CatalogGetDatasetToolHandler(
        ISqlExecutor sqlExecutor,
        IExecutionContextAccessor contextAccessor,
        ILogger<CatalogGetDatasetToolHandler> logger)
        : base(sqlExecutor, contextAccessor, logger)
    {
    }

    public override async Task<string> ExecuteAsync(
        ToolDefinition tool,
        string argumentsJson,
        TilsoftExecutionContext context,
        CancellationToken ct)
    {
        var args = ParseArguments<CatalogGetDatasetArgs>(argumentsJson);
        
        if (string.IsNullOrWhiteSpace(args?.DatasetKey))
        {
            throw new ArgumentException("DatasetKey is required for catalog_get_dataset");
        }

        var parameters = new Dictionary<string, object?>
        {
            ["TenantId"] = context.TenantId,
            ["DatasetKey"] = args.DatasetKey
        };

        return await ExecuteSpAsync("dbo.ai_catalog_get_dataset", parameters, ct);
    }

    private sealed class CatalogGetDatasetArgs
    {
        public string? DatasetKey { get; set; }
    }
}
