using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Tools;

namespace TILSOFTAI.Infrastructure.Tools;

public sealed class ToolCatalogSyncService : IToolCatalogResolver
{
    private readonly SqlOptions _sqlOptions;
    private readonly GovernanceOptions _governanceOptions;
    private readonly LocalizationOptions _localizationOptions;
    private readonly IExecutionContextAccessor _contextAccessor;
    private readonly IToolRegistry _toolRegistry;
    private readonly ILogger<ToolCatalogSyncService> _logger;

    public ToolCatalogSyncService(
        IOptions<SqlOptions> sqlOptions,
        IOptions<GovernanceOptions> governanceOptions,
        IOptions<LocalizationOptions> localizationOptions,
        IExecutionContextAccessor contextAccessor,
        IToolRegistry toolRegistry,
        ILogger<ToolCatalogSyncService> logger)
    {
        _sqlOptions = sqlOptions?.Value ?? throw new ArgumentNullException(nameof(sqlOptions));
        _governanceOptions = governanceOptions?.Value ?? throw new ArgumentNullException(nameof(governanceOptions));
        _localizationOptions = localizationOptions?.Value ?? throw new ArgumentNullException(nameof(localizationOptions));
        _contextAccessor = contextAccessor ?? throw new ArgumentNullException(nameof(contextAccessor));
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<ToolDefinition>> GetResolvedToolsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _contextAccessor.Current.TenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new InvalidOperationException("Execution context TenantId is required to resolve tools.");
        }

        var language = string.IsNullOrWhiteSpace(_contextAccessor.Current.Language)
            ? _localizationOptions.DefaultLanguage
            : _contextAccessor.Current.Language;
        var sqlTools = await LoadSqlToolsAsync(tenantId, language, _localizationOptions.DefaultLanguage, cancellationToken);
        var registryTools = _toolRegistry.ListEnabled();

        var allowlist = new HashSet<string>(_governanceOptions.ToolAllowlist ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        var enforceAllowlist = allowlist.Count > 0;

        var resolved = new List<ToolDefinition>();

        foreach (var tool in registryTools)
        {
            if (enforceAllowlist && !allowlist.Contains(tool.Name))
            {
                continue;
            }

            if (!sqlTools.TryGetValue(tool.Name, out var sqlTool))
            {
                _logger.LogWarning("Tool {ToolName} not found in ToolCatalog.", tool.Name);
                continue;
            }

            var merged = Merge(tool, sqlTool);
            if (merged is null)
            {
                continue;
            }

            resolved.Add(merged);
        }

        return resolved;
    }

    private ToolDefinition? Merge(ToolDefinition moduleTool, ToolCatalogEntry sqlTool)
    {
        if (!sqlTool.IsEnabled)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(moduleTool.SpName)
            && !string.Equals(moduleTool.SpName, sqlTool.SpName, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Tool {ToolName} SpName mismatch between module and catalog.", moduleTool.Name);
            return null;
        }

        var instruction = FirstNonEmpty(sqlTool.Instruction, moduleTool.Instruction);
        var jsonSchema = FirstNonEmpty(sqlTool.JsonSchema, moduleTool.JsonSchema);

        if (string.IsNullOrWhiteSpace(instruction) || string.IsNullOrWhiteSpace(jsonSchema))
        {
            _logger.LogWarning("Tool {ToolName} missing instruction or schema after merge.", moduleTool.Name);
            return null;
        }

        var requiredRoles = !string.IsNullOrWhiteSpace(sqlTool.RequiredRoles)
            ? SplitRoles(sqlTool.RequiredRoles)
            : moduleTool.RequiredRoles;

        return new ToolDefinition
        {
            Name = moduleTool.Name,
            Description = FirstNonEmpty(sqlTool.Description, moduleTool.Description),
            Instruction = instruction,
            JsonSchema = jsonSchema,
            SpName = FirstNonEmpty(sqlTool.SpName, moduleTool.SpName),
            RequiredRoles = requiredRoles,
            Module = moduleTool.Module,
            IsEnabled = moduleTool.IsEnabled && sqlTool.IsEnabled
        };
    }

    private async Task<Dictionary<string, ToolCatalogEntry>> LoadSqlToolsAsync(
        string tenantId,
        string language,
        string defaultLanguage,
        CancellationToken cancellationToken)
    {
        var results = new Dictionary<string, ToolCatalogEntry>(StringComparer.OrdinalIgnoreCase);

        await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand("dbo.app_toolcatalog_list", connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = _sqlOptions.CommandTimeoutSeconds
        };

        command.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.NVarChar, 50)
        {
            Value = tenantId
        });
        command.Parameters.Add(new SqlParameter("@Language", SqlDbType.NVarChar, 10)
        {
            Value = language
        });
        command.Parameters.Add(new SqlParameter("@DefaultLanguage", SqlDbType.NVarChar, 10)
        {
            Value = string.IsNullOrWhiteSpace(defaultLanguage) ? "en" : defaultLanguage
        });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var toolName = reader["ToolName"] as string;
            if (string.IsNullOrWhiteSpace(toolName))
            {
                continue;
            }

            var entry = new ToolCatalogEntry
            {
                ToolName = toolName,
                SpName = reader["SpName"] as string ?? string.Empty,
                IsEnabled = reader["IsEnabled"] != DBNull.Value && Convert.ToBoolean(reader["IsEnabled"]),
                RequiredRoles = reader["RequiredRoles"] as string,
                JsonSchema = reader["JsonSchema"] as string,
                Instruction = reader["Instruction"] as string,
                Description = reader["Description"] as string
            };

            results[toolName] = entry;
        }

        return results;
    }

    private static string FirstNonEmpty(string? primary, string? fallback)
    {
        if (!string.IsNullOrWhiteSpace(primary))
        {
            return primary;
        }

        return fallback ?? string.Empty;
    }

    private static string[] SplitRoles(string? roles)
    {
        if (string.IsNullOrWhiteSpace(roles))
        {
            return Array.Empty<string>();
        }

        return roles.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
