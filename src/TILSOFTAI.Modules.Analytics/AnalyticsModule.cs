using TILSOFTAI.Orchestration.Modules;
using TILSOFTAI.Orchestration.Tools;
using TILSOFTAI.Modules.Analytics.Tools;

namespace TILSOFTAI.Modules.Analytics;

/// <summary>
/// Analytics module for deep analytics workflow with Schema RAG.
/// </summary>
public sealed class AnalyticsModule : ITilsoftModule
{
    public string Name => "Analytics";

    public void Register(IToolRegistry toolRegistry, INamedToolHandlerRegistry handlerRegistry)
    {
        if (handlerRegistry is null)
        {
            throw new ArgumentNullException(nameof(handlerRegistry));
        }

        // Tools are registered from ToolCatalog via SQL seed
        // Only register handlers here
        
        handlerRegistry.Register("catalog_search", typeof(CatalogSearchToolHandler));
        handlerRegistry.Register("catalog_get_dataset", typeof(CatalogGetDatasetToolHandler));
        handlerRegistry.Register("analytics_validate_plan", typeof(AnalyticsValidatePlanToolHandler));
    }
}
