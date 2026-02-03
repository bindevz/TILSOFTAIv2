using System.Data;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Orchestration.Sql;

using TILSOFTAI.Domain.Telemetry;
using TILSOFTAI.Infrastructure.Telemetry;

namespace TILSOFTAI.Infrastructure.Sql;

public sealed class SqlExecutor : ISqlExecutor
{
    private readonly SqlOptions _sqlOptions;
    private readonly ITelemetryService _telemetry;
    private readonly TILSOFTAI.Domain.Resilience.ICircuitBreakerPolicy _circuitBreaker;
    private readonly TILSOFTAI.Domain.Resilience.IRetryPolicy _retryPolicy;

    public SqlExecutor(IOptions<SqlOptions> sqlOptions, ITelemetryService telemetry, TILSOFTAI.Infrastructure.Resilience.CircuitBreakerRegistry registry, TILSOFTAI.Infrastructure.Resilience.RetryPolicyRegistry retryRegistry)
    {
        _sqlOptions = sqlOptions?.Value ?? throw new ArgumentNullException(nameof(sqlOptions));
        _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        _circuitBreaker = registry?.GetOrCreate("sql") ?? throw new ArgumentNullException(nameof(registry));
        _retryPolicy = retryRegistry?.GetOrCreate("sql") ?? throw new ArgumentNullException(nameof(retryRegistry));
    }

    private const string ModelCallableSpPrefix = "ai_";

    public async Task<string> ExecuteToolAsync(string storedProcedure, string tenantId, string argumentsJson, CancellationToken cancellationToken = default)
    {
        if (!storedProcedure.StartsWith(ModelCallableSpPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Security Violation: Tools must start with '{ModelCallableSpPrefix}'. Attempted: {storedProcedure}");
        }

        using var activity = _telemetry.StartActivity(TelemetryConstants.Spans.SqlExecute, ActivityKind.Client);
        activity?.SetTag(TelemetryConstants.Attributes.SqlProcedure, storedProcedure);

        return await _circuitBreaker.ExecuteAsync<string>(async cbToken =>
        {
            return await _retryPolicy.ExecuteAsync<string>(async (attempt, ct) =>
            {
            await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
            await connection.OpenAsync(ct);

            await using var command = new SqlCommand(storedProcedure, connection)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = _sqlOptions.CommandTimeoutSeconds
            };

            command.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.NVarChar, 50) { Value = tenantId });
            command.Parameters.Add(new SqlParameter("@ArgsJson", SqlDbType.NVarChar, -1) { Value = argumentsJson });

                var result = await command.ExecuteScalarAsync(ct);
                return result == null || result == DBNull.Value ? string.Empty : Convert.ToString(result) ?? string.Empty;
            }, cbToken);
        }, cancellationToken);
    }

    public async Task<string> ExecuteWriteActionAsync(string storedProcedure, string tenantId, string argumentsJson, CancellationToken cancellationToken = default)
    {
        using var activity = _telemetry.StartActivity(TelemetryConstants.Spans.SqlExecute, ActivityKind.Client);
        activity?.SetTag(TelemetryConstants.Attributes.SqlProcedure, storedProcedure);

        return await _circuitBreaker.ExecuteAsync<string>(async cbToken =>
        {
            return await _retryPolicy.ExecuteAsync<string>(async (attempt, ct) =>
            {
            await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
            await connection.OpenAsync(ct);

            // Security Check: Verify SP is in the WriteActionCatalog and Enabled
            var checkCmd = new SqlCommand("SELECT 1 FROM dbo.WriteActionCatalog WHERE TenantId = @TenantId AND SpName = @SpName AND IsEnabled = 1", connection);
            checkCmd.Parameters.Add(new SqlParameter("@TenantId", tenantId));
            checkCmd.Parameters.Add(new SqlParameter("@SpName", storedProcedure));

            var exists = await checkCmd.ExecuteScalarAsync(ct);
            if (exists == null || exists == DBNull.Value)
            {
                 throw new InvalidOperationException($"Security Violation: Stored Procedure '{storedProcedure}' is not a valid/enabled Write Action for this tenant.");
            }

            await using var command = new SqlCommand(storedProcedure, connection)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = _sqlOptions.CommandTimeoutSeconds
            };

            command.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.NVarChar, 50) { Value = tenantId });
            command.Parameters.Add(new SqlParameter("@ArgsJson", SqlDbType.NVarChar, -1) { Value = argumentsJson });

                var result = await command.ExecuteScalarAsync(ct);
                return result == null || result == DBNull.Value ? string.Empty : Convert.ToString(result) ?? string.Empty;
            }, cbToken);
        }, cancellationToken);
    }

    public async Task<string> ExecuteAtomicPlanAsync(
        string storedProcedure,
        string tenantId,
        string planJson,
        string callerUserId,
        string callerRoles,
        CancellationToken cancellationToken = default)
    {
        using var activity = _telemetry.StartActivity(TelemetryConstants.Spans.SqlExecute, ActivityKind.Client);
        activity?.SetTag(TelemetryConstants.Attributes.SqlProcedure, storedProcedure);

        return await _circuitBreaker.ExecuteAsync<string>(async cbToken =>
        {
            return await _retryPolicy.ExecuteAsync<string>(async (attempt, ct) =>
            {
            await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
            await connection.OpenAsync(ct);

            await using var command = new SqlCommand(storedProcedure, connection)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = _sqlOptions.CommandTimeoutSeconds
            };

            command.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.NVarChar, 50) { Value = tenantId });
            command.Parameters.Add(new SqlParameter("@PlanJson", SqlDbType.NVarChar, -1) { Value = planJson });
            command.Parameters.Add(new SqlParameter("@CallerUserId", SqlDbType.NVarChar, 50) { Value = callerUserId });
            command.Parameters.Add(new SqlParameter("@CallerRoles", SqlDbType.NVarChar, 1000) { Value = callerRoles });

                var result = await command.ExecuteScalarAsync(ct);
                return result == null || result == DBNull.Value ? string.Empty : Convert.ToString(result) ?? string.Empty;
            }, cbToken);
        }, cancellationToken);
    }

    public async Task<string> ExecuteDiagnosticsAsync(
        string storedProcedure,
        string tenantId,
        string module,
        string ruleKey,
        string? inputJson,
        CancellationToken cancellationToken = default)
    {
        using var activity = _telemetry.StartActivity(TelemetryConstants.Spans.SqlExecute, ActivityKind.Client);
        activity?.SetTag(TelemetryConstants.Attributes.SqlProcedure, storedProcedure);

        return await _circuitBreaker.ExecuteAsync<string>(async cbToken =>
        {
            return await _retryPolicy.ExecuteAsync<string>(async (attempt, ct) =>
            {
            await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
            await connection.OpenAsync(ct);

            await using var command = new SqlCommand(storedProcedure, connection)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = _sqlOptions.CommandTimeoutSeconds
            };

            command.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.NVarChar, 50) { Value = tenantId });
            command.Parameters.Add(new SqlParameter("@Module", SqlDbType.NVarChar, 100) { Value = module });
            command.Parameters.Add(new SqlParameter("@RuleKey", SqlDbType.NVarChar, 200) { Value = ruleKey });
            command.Parameters.Add(new SqlParameter("@InputJson", SqlDbType.NVarChar, -1)
            {
                Value = string.IsNullOrWhiteSpace(inputJson) ? DBNull.Value : inputJson
            });

                var result = await command.ExecuteScalarAsync(ct);
                return result == null || result == DBNull.Value ? string.Empty : Convert.ToString(result) ?? string.Empty;
            }, cbToken);
        }, cancellationToken);
    }

    public async Task<string> ExecuteAsync(
        string storedProcedure,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storedProcedure))
        {
            throw new ArgumentException("Stored procedure is required.", nameof(storedProcedure));
        }

        using var activity = _telemetry.StartActivity(TelemetryConstants.Spans.SqlExecute, ActivityKind.Client);
        activity?.SetTag(TelemetryConstants.Attributes.SqlProcedure, storedProcedure);

        return await _circuitBreaker.ExecuteAsync<string>(async cbToken =>
        {
            return await _retryPolicy.ExecuteAsync<string>(async (attempt, ct) =>
            {
            await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
            await connection.OpenAsync(ct);

            await using var command = new SqlCommand(storedProcedure, connection)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = _sqlOptions.CommandTimeoutSeconds
            };

            foreach (var (name, value) in parameters)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                command.Parameters.Add(new SqlParameter(name, value ?? DBNull.Value));
            }

                var result = await command.ExecuteScalarAsync(ct);
                return result == null || result == DBNull.Value ? string.Empty : Convert.ToString(result) ?? string.Empty;
            }, cbToken);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ExecuteQueryAsync(
        string storedProcedure,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storedProcedure))
        {
            throw new ArgumentException("Stored procedure is required.", nameof(storedProcedure));
        }

        using var activity = _telemetry.StartActivity(TelemetryConstants.Spans.SqlExecute, ActivityKind.Client);
        activity?.SetTag(TelemetryConstants.Attributes.SqlProcedure, storedProcedure);

        return await _circuitBreaker.ExecuteAsync<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(async cbToken =>
        {
            return await _retryPolicy.ExecuteAsync<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(async (attempt, ct) =>
            {
            await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
            await connection.OpenAsync(ct);

            await using var command = new SqlCommand(storedProcedure, connection)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = _sqlOptions.CommandTimeoutSeconds
            };

            foreach (var (name, value) in parameters)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                command.Parameters.Add(new SqlParameter(name, value ?? DBNull.Value));
            }

            var rows = new List<IReadOnlyDictionary<string, object?>>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    row[reader.GetName(i)] = value;
                }
                rows.Add(row);
            }

                return rows;
            }, cbToken);
        }, cancellationToken);
    }
}
