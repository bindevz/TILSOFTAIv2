namespace TILSOFTAI.Infrastructure.Catalog;

public interface IPlatformCatalogPromotionGate
{
    Task<CatalogPromotionGateResult> EvaluateAsync(
        CatalogPromotionGateRequest request,
        CatalogMutationContext context,
        CancellationToken ct);

    IReadOnlyList<CatalogControlPlaneSloDefinition> GetSloDefinitions();
}
