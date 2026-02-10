using System.Text;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Prompting;
using TILSOFTAI.Orchestration.Sql;

namespace TILSOFTAI.Infrastructure.Metadata;

/// <summary>
/// PATCH 36.03: Implements IScopedContextPackProvider — no mutable singleton state.
/// Module scope is received via PromptBuildContext.ResolvedModules.
/// </summary>
public sealed class MetadataDictionaryContextPackProvider : IScopedContextPackProvider
{
    private const string ContextPackKey = "metadata_dictionary";
    private readonly ISqlExecutor _sqlExecutor;
    private readonly LocalizationOptions _localizationOptions;

    public MetadataDictionaryContextPackProvider(
        ISqlExecutor sqlExecutor,
        IOptions<LocalizationOptions> localizationOptions)
    {
        _sqlExecutor = sqlExecutor ?? throw new ArgumentNullException(nameof(sqlExecutor));
        _localizationOptions = localizationOptions?.Value ?? throw new ArgumentNullException(nameof(localizationOptions));
    }

    /// <summary>
    /// Legacy IContextPackProvider — unscoped (backward compat).
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> GetContextPacksAsync(
        TilsoftExecutionContext context,
        CancellationToken cancellationToken)
    {
        return await GetContextPacksInternalAsync(context, modules: null, cancellationToken);
    }

    /// <summary>
    /// PATCH 36.03: Scoped provider — uses buildContext.ResolvedModules (no mutable state).
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> GetContextPacksAsync(
        TilsoftExecutionContext context,
        PromptBuildContext buildContext,
        CancellationToken cancellationToken)
    {
        var modules = buildContext.ResolvedModules is { Count: > 0 }
            ? buildContext.ResolvedModules
            : null;
        return await GetContextPacksInternalAsync(context, modules, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<string, string>> GetContextPacksInternalAsync(
        TilsoftExecutionContext context,
        IReadOnlyList<string>? modules,
        CancellationToken cancellationToken)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var resolvedLanguage = string.IsNullOrWhiteSpace(context.Language)
            ? _localizationOptions.DefaultLanguage
            : context.Language;

        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows;

        if (modules is { Count: > 0 })
        {
            // Scoped: use module-filtered SP
            var scopedParams = new Dictionary<string, object?>
            {
                ["@TenantId"] = string.IsNullOrWhiteSpace(context.TenantId) ? null : context.TenantId,
                ["@Language"] = resolvedLanguage,
                ["@DefaultLanguage"] = _localizationOptions.DefaultLanguage,
                ["@ModulesJson"] = System.Text.Json.JsonSerializer.Serialize(modules)
            };
            rows = await _sqlExecutor.ExecuteQueryAsync("dbo.app_metadatadictionary_list_scoped", scopedParams, cancellationToken);

            // PATCH 36.03: Language fallback — if no rows for requested language, try default
            if (rows.Count == 0
                && !string.Equals(resolvedLanguage, _localizationOptions.DefaultLanguage, StringComparison.OrdinalIgnoreCase))
            {
                scopedParams["@Language"] = _localizationOptions.DefaultLanguage;
                rows = await _sqlExecutor.ExecuteQueryAsync("dbo.app_metadatadictionary_list_scoped", scopedParams, cancellationToken);
            }
        }
        else
        {
            // Unscoped: backward compatible
            var parameters = new Dictionary<string, object?>
            {
                ["@TenantId"] = string.IsNullOrWhiteSpace(context.TenantId) ? null : context.TenantId,
                ["@Language"] = resolvedLanguage,
                ["@DefaultLanguage"] = _localizationOptions.DefaultLanguage
            };
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
