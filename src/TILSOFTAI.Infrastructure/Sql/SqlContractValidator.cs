using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Infrastructure.Sql;

public sealed class SqlContractValidator
{
    private static readonly string[] RequiredAppProcedures =
    [
        "app_errorlog_insert",
        "app_conversation_upsert",
        "app_conversationmessage_insert",
        "app_toolexecution_insert",
        "app_toolcatalog_list",
        "app_metadatadictionary_list"
    ];

    private readonly SqlOptions _sqlOptions;
    private readonly ILogger<SqlContractValidator> _logger;

    public SqlContractValidator(IOptions<SqlOptions> sqlOptions, ILogger<SqlContractValidator> logger)
    {
        _sqlOptions = sqlOptions?.Value ?? throw new ArgumentNullException(nameof(sqlOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ValidateAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var procedures = await LoadProceduresAsync(connection, cancellationToken);
        var toolProcedures = await LoadToolProceduresAsync(connection, cancellationToken);

        var errors = new List<string>();

        foreach (var required in RequiredAppProcedures)
        {
            if (!procedures.Contains(required))
            {
                errors.Add($"Required stored procedure missing: {required}");
            }
            else if (!required.StartsWith("app_", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Required app procedure has invalid prefix: {required}");
            }
        }

        foreach (var spName in toolProcedures)
        {
            if (!spName.StartsWith("ai_", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"ToolCatalog SP must start with ai_: {spName}");
            }

            if (!procedures.Contains(spName) && !procedures.Contains(NormalizeName(spName)))
            {
                errors.Add($"ToolCatalog SP not found in sys.procedures: {spName}");
            }
        }

        if (errors.Count > 0)
        {
            var message = string.Join(Environment.NewLine, errors);
            _logger.LogError("SQL contract validation failed:{NewLine}{Message}", Environment.NewLine, message);
            throw new InvalidOperationException($"SQL contract validation failed:{Environment.NewLine}{message}");
        }

        _logger.LogInformation("SQL contract validation succeeded.");
    }

    private static async Task<HashSet<string>> LoadProceduresAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        const string query = """
SELECT s.name AS SchemaName, p.name AS ProcName
FROM sys.procedures p
INNER JOIN sys.schemas s ON p.schema_id = s.schema_id
""";

        await using var command = new SqlCommand(query, connection) { CommandType = CommandType.Text };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var schema = reader.GetString(0);
            var name = reader.GetString(1);
            result.Add(name);
            result.Add($"{schema}.{name}");
        }

        return result;
    }

    private static async Task<List<string>> LoadToolProceduresAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        var result = new List<string>();
        const string query = """
SELECT SpName
FROM dbo.ToolCatalog
WHERE IsEnabled = 1
  AND SpName IS NOT NULL
""";

        await using var command = new SqlCommand(query, connection) { CommandType = CommandType.Text };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var spName = reader.GetString(0);
            if (!string.IsNullOrWhiteSpace(spName))
            {
                result.Add(spName);
            }
        }

        return result;
    }

    private static string NormalizeName(string spName)
    {
        var parts = spName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 2 ? parts[1] : spName;
    }
}
