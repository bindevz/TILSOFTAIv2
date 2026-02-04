using System.Data;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Telemetry;
using TILSOFTAI.Orchestration.Sql;

namespace TILSOFTAI.Infrastructure.Sql;

public sealed partial class SqlExecutor : ISqlExecutor
{
    private readonly SqlOptions _sqlOptions;
    private readonly ITelemetryService _telemetry;
    private readonly Domain.Resilience.ICircuitBreakerPolicy _circuitBreaker;
    private readonly Domain.Resilience.IRetryPolicy _retryPolicy;
    private readonly string _pooledConnectionString;

    private const string ModelCallableSpPrefix = ConfigurationDefaults.Sql.ModelCallableSpPrefix;

    public SqlExecutor(
        IOptions<SqlOptions> sqlOptions,
        ITelemetryService telemetry,
        Resilience.CircuitBreakerRegistry registry,
        Resilience.RetryPolicyRegistry retryRegistry)
    {
        _sqlOptions = sqlOptions?.Value ?? throw new ArgumentNullException(nameof(sqlOptions));
        _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        _circuitBreaker = registry?.GetOrCreate("sql") ?? throw new ArgumentNullException(nameof(registry));
        _retryPolicy = retryRegistry?.GetOrCreate("sql") ?? throw new ArgumentNullException(nameof(retryRegistry));
        _pooledConnectionString = _sqlOptions.GetPooledConnectionString();
    }

    #region Core Execution Pattern

    /// <summary>
    /// Core execution method with resilience patterns (circuit breaker + retry).
    /// All public methods delegate to this for consistent behavior.
    /// </summary>
    private async Task<T> ExecuteWithResilienceAsync<T>(
        string storedProcedure,
        Action<SqlCommand> configureParameters,
        Func<SqlCommand, CancellationToken, Task<T>> executeCommand,
        CancellationToken cancellationToken)
    {
        using var activity = _telemetry.StartActivity(
            TelemetryConstants.Spans.SqlExecute,
            ActivityKind.Client);
        activity?.SetTag(TelemetryConstants.Attributes.SqlProcedure, storedProcedure);

        return await _circuitBreaker.ExecuteAsync(async cbToken =>
        {
            return await _retryPolicy.ExecuteAsync(async (attempt, ct) =>
            {
                await using var connection = new SqlConnection(_pooledConnectionString);
                await connection.OpenAsync(ct);

                await using var command = CreateCommand(storedProcedure, connection);
                configureParameters(command);

                return await executeCommand(command, ct);
            }, cbToken);
        }, cancellationToken);
    }

    /// <summary>
    /// Creates a configured SqlCommand with standard settings.
    /// </summary>
    private SqlCommand CreateCommand(string storedProcedure, SqlConnection connection)
    {
        return new SqlCommand(storedProcedure, connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = _sqlOptions.CommandTimeoutSeconds
        };
    }

    /// <summary>
    /// Executes command and returns scalar as string.
    /// </summary>
    private static async Task<string> ExecuteScalarStringAsync(
        SqlCommand command,
        CancellationToken ct)
    {
        var result = await command.ExecuteScalarAsync(ct);
        return result == null || result == DBNull.Value
            ? string.Empty
            : Convert.ToString(result) ?? string.Empty;
    }

    #endregion

    #region Validation Helpers

    /// <summary>
    /// Validates that stored procedure name starts with ai_ prefix.
    /// </summary>
    private void ValidateToolSpName(string storedProcedure)
    {
        if (!storedProcedure.StartsWith(ModelCallableSpPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Security Violation: Tools must start with '{ModelCallableSpPrefix}'. Attempted: {storedProcedure}");
        }
    }

    /// <summary>
    /// Validates that stored procedure is in WriteActionCatalog and enabled.
    /// </summary>
    private static async Task ValidateWriteActionAsync(
        SqlConnection connection,
        string storedProcedure,
        string tenantId,
        CancellationToken ct)
    {
        const string checkSql = @"
            SELECT 1 FROM dbo.WriteActionCatalog 
            WHERE TenantId = @TenantId 
              AND SpName = @SpName 
              AND IsEnabled = 1";

        await using var checkCmd = new SqlCommand(checkSql, connection);
        checkCmd.Parameters.AddWithValue("@TenantId", tenantId);
        checkCmd.Parameters.AddWithValue("@SpName", storedProcedure);

        var exists = await checkCmd.ExecuteScalarAsync(ct);
        if (exists == null || exists == DBNull.Value)
        {
            throw new InvalidOperationException(
                $"Security Violation: '{storedProcedure}' is not a valid/enabled Write Action for tenant.");
        }
    }

    #endregion

    #region Public API Methods

    public Task<string> ExecuteToolAsync(
        string storedProcedure,
        string tenantId,
        string argumentsJson,
        CancellationToken cancellationToken = default)
    {
        ValidateToolSpName(storedProcedure);

        return ExecuteWithResilienceAsync(
            storedProcedure,
            cmd =>
            {
                cmd.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.NVarChar, 50) { Value = tenantId });
                cmd.Parameters.Add(new SqlParameter("@ArgsJson", SqlDbType.NVarChar, -1) { Value = argumentsJson });
            },
            ExecuteScalarStringAsync,
            cancellationToken);
    }

    public Task<string> ExecuteWriteActionAsync(
        string storedProcedure,
        string tenantId,
        string argumentsJson,
        CancellationToken cancellationToken = default)
    {
        return ExecuteWithResilienceAsync(
            storedProcedure,
            cmd =>
            {
                cmd.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.NVarChar, 50) { Value = tenantId });
                cmd.Parameters.Add(new SqlParameter("@ArgsJson", SqlDbType.NVarChar, -1) { Value = argumentsJson });
            },
            async (cmd, ct) =>
            {
                await ValidateWriteActionAsync(cmd.Connection!, storedProcedure, tenantId, ct);
                return await ExecuteScalarStringAsync(cmd, ct);
            },
            cancellationToken);
    }

    public Task<string> ExecuteAtomicPlanAsync(
        string storedProcedure,
        string tenantId,
        string planJson,
        string callerUserId,
        string callerRoles,
        CancellationToken cancellationToken = default)
    {
        return ExecuteWithResilienceAsync(
            storedProcedure,
            cmd =>
            {
                cmd.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.NVarChar, 50) { Value = tenantId });
                cmd.Parameters.Add(new SqlParameter("@PlanJson", SqlDbType.NVarChar, -1) { Value = planJson });
                cmd.Parameters.Add(new SqlParameter("@CallerUserId", SqlDbType.NVarChar, 50) { Value = callerUserId });
                cmd.Parameters.Add(new SqlParameter("@CallerRoles", SqlDbType.NVarChar, 1000) { Value = callerRoles });
            },
            ExecuteScalarStringAsync,
            cancellationToken);
    }

    public Task<string> ExecuteDiagnosticsAsync(
        string storedProcedure,
        string tenantId,
        string module,
        string ruleKey,
        string? inputJson,
        CancellationToken cancellationToken = default)
    {
        return ExecuteWithResilienceAsync(
            storedProcedure,
            cmd =>
            {
                cmd.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.NVarChar, 50) { Value = tenantId });
                cmd.Parameters.Add(new SqlParameter("@Module", SqlDbType.NVarChar, 100) { Value = module });
                cmd.Parameters.Add(new SqlParameter("@RuleKey", SqlDbType.NVarChar, 200) { Value = ruleKey });
                cmd.Parameters.Add(new SqlParameter("@InputJson", SqlDbType.NVarChar, -1)
                {
                    Value = string.IsNullOrWhiteSpace(inputJson) ? DBNull.Value : inputJson
                });
            },
            ExecuteScalarStringAsync,
            cancellationToken);
    }

    public Task<string> ExecuteAsync(
        string storedProcedure,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storedProcedure))
        {
            throw new ArgumentException("Stored procedure is required.", nameof(storedProcedure));
        }

        return ExecuteWithResilienceAsync(
            storedProcedure,
            cmd =>
            {
                foreach (var (name, value) in parameters)
                {
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        cmd.Parameters.Add(new SqlParameter(name, value ?? DBNull.Value));
                    }
                }
            },
            ExecuteScalarStringAsync,
            cancellationToken);
    }

    public Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ExecuteQueryAsync(
        string storedProcedure,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storedProcedure))
        {
            throw new ArgumentException("Stored procedure is required.", nameof(storedProcedure));
        }

        return ExecuteWithResilienceAsync(
            storedProcedure,
            cmd =>
            {
                foreach (var (name, value) in parameters)
                {
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        cmd.Parameters.Add(new SqlParameter(name, value ?? DBNull.Value));
                    }
                }
            },
            ExecuteReaderAsync,
            cancellationToken);
    }

    private static async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ExecuteReaderAsync(
        SqlCommand command,
        CancellationToken ct)
    {
        var rows = new List<IReadOnlyDictionary<string, object?>>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        
        while (await reader.ReadAsync(ct))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            rows.Add(row);
        }

        return rows;
    }

    #endregion
}
