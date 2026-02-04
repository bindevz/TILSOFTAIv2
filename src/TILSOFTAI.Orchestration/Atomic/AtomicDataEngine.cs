using System.Text.Json;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Domain.Properties;
using TILSOFTAI.Orchestration.Planning;
using TILSOFTAI.Orchestration.Sql;

namespace TILSOFTAI.Orchestration.Atomic;

public sealed class AtomicDataEngine
{
    private const string AtomicStoredProcedure = "ai_atomic_execute_plan";
    private readonly PlanOptimizer _planOptimizer;
    private readonly IAtomicCatalogProvider _catalogProvider;
    private readonly ISqlExecutor _sqlExecutor;

    public AtomicDataEngine(PlanOptimizer planOptimizer, IAtomicCatalogProvider catalogProvider, ISqlExecutor sqlExecutor)
    {
        _planOptimizer = planOptimizer ?? throw new ArgumentNullException(nameof(planOptimizer));
        _catalogProvider = catalogProvider ?? throw new ArgumentNullException(nameof(catalogProvider));
        _sqlExecutor = sqlExecutor ?? throw new ArgumentNullException(nameof(sqlExecutor));
    }

    public async Task<JsonDocument> ExecuteAsync(string planJson, TilsoftExecutionContext ctx, CancellationToken ct)
    {
        _ = _catalogProvider;
        var validation = await _planOptimizer.ValidateAsync(planJson, ctx, ct);
        if (!validation.IsValid || validation.NormalizedPlan is null)
        {
            throw new InvalidOperationException(validation.Error ?? Resources.Ex_PlanValidationFailed);
        }

        var normalizedPlanJson = validation.NormalizedPlan.RootElement.GetRawText();
        var callerRoles = ctx.Roles?.Length > 0 ? string.Join(",", ctx.Roles) : string.Empty;

        var resultJson = await _sqlExecutor.ExecuteAtomicPlanAsync(
            AtomicStoredProcedure,
            ctx.TenantId,
            normalizedPlanJson,
            ctx.UserId,
            callerRoles,
            ct);

        if (string.IsNullOrWhiteSpace(resultJson))
        {
            resultJson = "{}";
        }

        return JsonDocument.Parse(resultJson);
    }
}
