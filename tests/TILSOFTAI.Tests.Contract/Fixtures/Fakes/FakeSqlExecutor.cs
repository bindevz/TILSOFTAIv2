using TILSOFTAI.Orchestration.Sql;

namespace TILSOFTAI.Tests.Contract.Fixtures.Fakes;

/// <summary>
/// Fake ISqlExecutor for contract tests that returns deterministic JSON responses.
/// No actual database calls are made.
/// </summary>
public sealed class FakeSqlExecutor : ISqlExecutor
{
    public Task<string> ExecuteToolAsync(string storedProcedure, string tenantId, string argumentsJson, CancellationToken cancellationToken = default)
    {
        // Return minimal valid JSON result for tool execution
        return Task.FromResult("{\"status\":\"success\",\"result\":[]}");
    }

    public Task<string> ExecuteAtomicPlanAsync(string storedProcedure, string tenantId, string planJson, string callerUserId, string callerRoles, CancellationToken cancellationToken = default)
    {
        // Return minimal valid JSON result for atomic plan execution
        return Task.FromResult("{\"status\":\"success\",\"rows\":[]}");
    }

    public Task<string> ExecuteDiagnosticsAsync(string storedProcedure, string tenantId, string module, string ruleKey, string? inputJson, CancellationToken cancellationToken = default)
    {
        // Return minimal valid JSON result for diagnostics
        return Task.FromResult("{\"status\":\"success\",\"diagnostics\":[]}");
    }

    public Task<string> ExecuteAsync(string storedProcedure, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken = default)
    {
        // Return minimal valid JSON result for generic execution
        return Task.FromResult("{\"status\":\"success\"}");
    }

    public Task<string> ExecuteWriteActionAsync(string storedProcedure, string tenantId, string argumentsJson, CancellationToken cancellationToken = default)
    {
        // Return minimal valid JSON result for write actions
        return Task.FromResult("{\"status\":\"success\",\"affectedRows\":0}");
    }

    public Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ExecuteQueryAsync(string storedProcedure, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken = default)
    {
        // Return empty result set for queries
        var emptyResults = new List<IReadOnlyDictionary<string, object?>>();
        return Task.FromResult<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(emptyResults);
    }
}
