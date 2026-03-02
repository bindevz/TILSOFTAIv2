using TILSOFTAI.Orchestration.Modules;
using TILSOFTAI.Orchestration.Tools;
using TILSOFTAI.Modules.Analytics.Tools;

namespace TILSOFTAI.Modules.Analytics;

/// <summary>
/// Analytics module for deep analytics workflow with Schema RAG.
/// PATCH 37.02: Registers SQL-backed tool stubs so ToolCatalogSyncService
/// can resolve analytics tools from SQL ToolCatalog.
/// </summary>
public sealed class AnalyticsModule : ITilsoftModule
{
    public string Name => "Analytics";

    public void Register(IToolRegistry toolRegistry, INamedToolHandlerRegistry handlerRegistry)
    {
        if (toolRegistry is null)
        {
            throw new ArgumentNullException(nameof(toolRegistry));
        }

        if (handlerRegistry is null)
        {
            throw new ArgumentNullException(nameof(handlerRegistry));
        }

        // PATCH 37.02: Register SQL-backed tool stubs
        // Instruction/JsonSchema come from SQL seed (001_seed_toolcatalog_analytics.sql)
        toolRegistry.Register(new ToolDefinition
        {
            Name = "catalog_search",
            SpName = "ai_catalog_search",
            Module = "Analytics",
            IsEnabled = true,
            IsSqlBacked = true
        });

        toolRegistry.Register(new ToolDefinition
        {
            Name = "catalog_get_dataset",
            SpName = "ai_catalog_get_dataset",
            Module = "Analytics",
            IsEnabled = true,
            IsSqlBacked = true
        });

        toolRegistry.Register(new ToolDefinition
        {
            Name = "analytics_validate_plan",
            SpName = "ai_analytics_validate_plan",
            Module = "Analytics",
            IsEnabled = true,
            IsSqlBacked = true
        });

        toolRegistry.Register(new ToolDefinition
        {
            Name = "analytics_execute_plan",
            SpName = "ai_analytics_execute_plan",
            Module = "Analytics",
            IsEnabled = true,
            IsSqlBacked = true
        });

        // Register handlers
        handlerRegistry.Register("catalog_search", typeof(CatalogSearchToolHandler));
        handlerRegistry.Register("catalog_get_dataset", typeof(CatalogGetDatasetToolHandler));
        handlerRegistry.Register("analytics_validate_plan", typeof(AnalyticsValidatePlanToolHandler));
        handlerRegistry.Register("analytics_execute_plan", typeof(AnalyticsExecutePlanToolHandler));
    }
}
