using System.Text;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Prompting;
using TILSOFTAI.Orchestration.Sql;

namespace TILSOFTAI.Infrastructure.Prompting;

public sealed class AtomicCatalogContextPackProvider : IContextPackProvider
{
    private const string ContextPackKey = "atomic_catalog";
    private const int MaxDatasets = 20;
    private const int MaxFieldsPerDataset = 10;

    private readonly ISqlExecutor _sqlExecutor;
    private readonly LocalizationOptions _localizationOptions;

    public AtomicCatalogContextPackProvider(
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

        // Fetch dataset catalog
        var datasetParams = new Dictionary<string, object?>
        {
            ["@TenantId"] = string.IsNullOrWhiteSpace(context.TenantId) ? null : context.TenantId
        };

        var datasets = await _sqlExecutor.ExecuteQueryAsync("dbo.app_catalog_dataset_list", datasetParams, cancellationToken);

        if (datasets.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        var builder = new StringBuilder();
        builder.AppendLine("Available Datasets:");

        var limitedDatasets = datasets.Take(MaxDatasets).ToList();

        foreach (var dataset in limitedDatasets)
        {
            var datasetCode = GetString(dataset, "DatasetCode");
            var datasetName = GetString(dataset, "DatasetName");

            if (string.IsNullOrWhiteSpace(datasetCode))
            {
                continue;
            }

            builder.AppendLine();
            builder.Append("- ").Append(datasetCode);

            if (!string.IsNullOrWhiteSpace(datasetName))
            {
                builder.Append(": ").Append(datasetName);
            }

            // Fetch fields for this dataset
            var fieldParams = new Dictionary<string, object?>
            {
                ["@TenantId"] = string.IsNullOrWhiteSpace(context.TenantId) ? null : context.TenantId,
                ["@DatasetKey"] = datasetCode
            };

            var fields = await _sqlExecutor.ExecuteQueryAsync("dbo.app_catalog_field_list", fieldParams, cancellationToken);
            var limitedFields = fields.Take(MaxFieldsPerDataset).ToList();

            if (limitedFields.Count > 0)
            {
                builder.AppendLine();
                builder.Append("  Fields: ");

                var fieldList = limitedFields
                    .Select(f => GetString(f, "FieldCode"))
                    .Where(f => !string.IsNullOrWhiteSpace(f));

                builder.Append(string.Join(", ", fieldList));
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
