using System.Text;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Prompting;
using TILSOFTAI.Orchestration.Sql;

namespace TILSOFTAI.Infrastructure.Metadata;

public sealed class MetadataDictionaryContextPackProvider : IContextPackProvider
{
    private const string ContextPackKey = "metadata_dictionary";
    private readonly ISqlExecutor _sqlExecutor;
    private readonly LocalizationOptions _localizationOptions;

    /// <summary>
    /// Current module scope. When set, metadata is filtered by module.
    /// Set by ChatPipeline before calling GetContextPacksAsync.
    /// Thread-safety: ChatPipeline creates new scope per request via DI scoping.
    /// </summary>
    public IReadOnlyList<string>? CurrentScope { get; set; }

    public MetadataDictionaryContextPackProvider(
        ISqlExecutor sqlExecutor,
        IOptions<LocalizationOptions> localizationOptions)
    {
        _sqlExecutor = sqlExecutor ?? throw new ArgumentNullException(nameof(sqlExecutor));
        _localizationOptions = localizationOptions?.Value ?? throw new ArgumentNullException(nameof(localizationOptions));
    }

    public async Task<IReadOnlyDictionary<string, string>> GetContextPacksAsync(
        TilsoftExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var resolvedLanguage = string.IsNullOrWhiteSpace(context.Language)
            ? _localizationOptions.DefaultLanguage
            : context.Language;

        var parameters = new Dictionary<string, object?>
        {
            ["@TenantId"] = string.IsNullOrWhiteSpace(context.TenantId) ? null : context.TenantId,
            ["@Language"] = resolvedLanguage,
            ["@DefaultLanguage"] = _localizationOptions.DefaultLanguage
        };

        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows;

        if (CurrentScope is { Count: > 0 })
        {
            // Scoped: use module-filtered SP
            var scopedParams = new Dictionary<string, object?>
            {
                ["@TenantId"] = string.IsNullOrWhiteSpace(context.TenantId) ? null : context.TenantId,
                ["@Language"] = resolvedLanguage,
                ["@DefaultLanguage"] = _localizationOptions.DefaultLanguage,
                ["@ModulesJson"] = System.Text.Json.JsonSerializer.Serialize(CurrentScope)
            };
            rows = await _sqlExecutor.ExecuteQueryAsync("dbo.app_metadatadictionary_list_scoped", scopedParams, cancellationToken);
        }
        else
        {
            // Unscoped: backward compatible
            rows = await _sqlExecutor.ExecuteQueryAsync("dbo.app_metadatadictionary_list", parameters, cancellationToken);
        }

        if (rows.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        var entries = rows
            .Select(row => new
            {
                Key = GetString(row, "Key"),
                DisplayName = GetString(row, "DisplayName"),
                Description = GetString(row, "Description"),
                Unit = GetString(row, "Unit"),
                Examples = GetString(row, "Examples")
            })
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Key))
            .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (entries.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        var builder = new StringBuilder();
        foreach (var entry in entries)
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append(entry.Key).Append(": ").Append(entry.DisplayName);

            if (!string.IsNullOrWhiteSpace(entry.Description))
            {
                builder.Append(" - ").Append(entry.Description);
            }

            if (!string.IsNullOrWhiteSpace(entry.Unit))
            {
                builder.Append(" [Unit: ").Append(entry.Unit).Append(']');
            }

            if (!string.IsNullOrWhiteSpace(entry.Examples))
            {
                builder.Append(" Example: ").Append(entry.Examples);
            }
        }

        return new Dictionary<string, string>
        {
            [ContextPackKey] = builder.ToString()
        };
    }

    private static string GetString(IReadOnlyDictionary<string, object?> row, string key)
    {
        return row.TryGetValue(key, out var value) && value is not null
            ? Convert.ToString(value) ?? string.Empty
            : string.Empty;
    }
}
