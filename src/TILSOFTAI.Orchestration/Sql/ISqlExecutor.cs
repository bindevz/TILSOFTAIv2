namespace TILSOFTAI.Orchestration.Sql;

public interface ISqlExecutor
{
    Task<string> ExecuteToolAsync(string storedProcedure, string tenantId, string argumentsJson, CancellationToken cancellationToken = default);
    Task<string> ExecuteAtomicPlanAsync(string storedProcedure, string tenantId, string planJson, string callerUserId, string callerRoles, CancellationToken cancellationToken = default);
    Task<string> ExecuteDiagnosticsAsync(string storedProcedure, string tenantId, string module, string ruleKey, string? inputJson, CancellationToken cancellationToken = default);
    Task<string> ExecuteAsync(string storedProcedure, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken = default);
    Task<string> ExecuteWriteActionAsync(string storedProcedure, string tenantId, string argumentsJson, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ExecuteQueryAsync(string storedProcedure, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken = default);
}
