using System.Text.Json;

namespace TILSOFTAI.Orchestration.Planning;

public sealed record PlanValidationResult(bool IsValid, string? Error, JsonDocument? NormalizedPlan)
{
    public static PlanValidationResult Success(JsonDocument normalizedPlan) => new(true, null, normalizedPlan);
    public static PlanValidationResult Fail(string error) => new(false, error, null);
}
