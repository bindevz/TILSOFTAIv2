using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Text.Json;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Infrastructure.Caching;
using TILSOFTAI.Orchestration.Atomic;

namespace TILSOFTAI.Infrastructure.Atomic;

public sealed class SqlAtomicCatalogProvider : IAtomicCatalogProvider
{
    private readonly SqlOptions _sqlOptions;
    private readonly SemanticCache _semanticCache;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SqlAtomicCatalogProvider(IOptions<SqlOptions> sqlOptions, SemanticCache semanticCache)
    {
        _sqlOptions = sqlOptions?.Value ?? throw new ArgumentNullException(nameof(sqlOptions));
        _semanticCache = semanticCache ?? throw new ArgumentNullException(nameof(semanticCache));
    }

    public async Task<IReadOnlyList<DatasetCatalogEntry>> GetDatasetsAsync(string tenantId, CancellationToken cancellationToken)
    {
        var key = SemanticCache.BuildKey(
            tenantId,
            "atomic",
            "atomic_catalog_snapshot",
            SemanticCache.ComputeHash("datasets"));

        return await _semanticCache.GetOrAddAsync(
            key,
            async () =>
            {
                var results = new List<DatasetCatalogEntry>();

                await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
                await connection.OpenAsync(cancellationToken);

                await using var command = new SqlCommand("dbo.app_catalog_dataset_list", connection)
                {
                    CommandType = CommandType.StoredProcedure,
                    CommandTimeout = _sqlOptions.CommandTimeoutSeconds
                };

                command.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.NVarChar, 50) { Value = tenantId });

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    results.Add(new DatasetCatalogEntry
                    {
                        DatasetKey = reader["DatasetKey"] as string ?? string.Empty,
                        TenantId = reader["TenantId"] as string,
                        BaseObject = reader["BaseObject"] as string,
                        TimeColumn = reader["TimeColumn"] as string,
                        IsEnabled = reader["IsEnabled"] != DBNull.Value && Convert.ToBoolean(reader["IsEnabled"])
                    });
                }

                return results;
            },
            results => JsonSerializer.Serialize(results, JsonOptions),
            json => JsonSerializer.Deserialize<List<DatasetCatalogEntry>>(json, JsonOptions),
            cancellationToken);
    }

    public async Task<IReadOnlyList<FieldCatalogEntry>> GetFieldsAsync(string tenantId, string datasetKey, CancellationToken cancellationToken)
    {
        var key = SemanticCache.BuildKey(
            tenantId,
            "atomic",
            "atomic_catalog_snapshot",
            SemanticCache.ComputeHash("fields", datasetKey));

        return await _semanticCache.GetOrAddAsync(
            key,
            async () =>
            {
                var results = new List<FieldCatalogEntry>();

                await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
                await connection.OpenAsync(cancellationToken);

                await using var command = new SqlCommand("dbo.app_catalog_field_list", connection)
                {
                    CommandType = CommandType.StoredProcedure,
                    CommandTimeout = _sqlOptions.CommandTimeoutSeconds
                };

                command.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.NVarChar, 50) { Value = tenantId });
                command.Parameters.Add(new SqlParameter("@DatasetKey", SqlDbType.NVarChar, 200) { Value = datasetKey });

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    results.Add(new FieldCatalogEntry
                    {
                        DatasetKey = reader["DatasetKey"] as string ?? string.Empty,
                        FieldKey = reader["FieldKey"] as string ?? string.Empty,
                        PhysicalColumn = reader["PhysicalColumn"] as string ?? string.Empty,
                        DataType = reader["DataType"] as string ?? string.Empty,
                        IsMetric = reader["IsMetric"] != DBNull.Value && Convert.ToBoolean(reader["IsMetric"]),
                        IsDimension = reader["IsDimension"] != DBNull.Value && Convert.ToBoolean(reader["IsDimension"]),
                        AllowedAggregations = reader["AllowedAggregations"] as string,
                        IsFilterable = reader["IsFilterable"] != DBNull.Value && Convert.ToBoolean(reader["IsFilterable"]),
                        IsGroupable = reader["IsGroupable"] != DBNull.Value && Convert.ToBoolean(reader["IsGroupable"]),
                        IsSortable = reader["IsSortable"] != DBNull.Value && Convert.ToBoolean(reader["IsSortable"]),
                        IsEnabled = reader["IsEnabled"] != DBNull.Value && Convert.ToBoolean(reader["IsEnabled"])
                    });
                }

                return results;
            },
            results => JsonSerializer.Serialize(results, JsonOptions),
            json => JsonSerializer.Deserialize<List<FieldCatalogEntry>>(json, JsonOptions),
            cancellationToken);
    }

    public async Task<IReadOnlyList<EntityGraphCatalogEntry>> GetEntityGraphsAsync(string tenantId, CancellationToken cancellationToken)
    {
        var key = SemanticCache.BuildKey(
            tenantId,
            "atomic",
            "atomic_catalog_snapshot",
            SemanticCache.ComputeHash("graphs"));

        return await _semanticCache.GetOrAddAsync(
            key,
            async () =>
            {
                var results = new List<EntityGraphCatalogEntry>();

                await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
                await connection.OpenAsync(cancellationToken);

                await using var command = new SqlCommand("dbo.app_catalog_entitygraph_list", connection)
                {
                    CommandType = CommandType.StoredProcedure,
                    CommandTimeout = _sqlOptions.CommandTimeoutSeconds
                };

                command.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.NVarChar, 50) { Value = tenantId });

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    results.Add(new EntityGraphCatalogEntry
                    {
                        GraphKey = reader["GraphKey"] as string ?? string.Empty,
                        FromDatasetKey = reader["FromDatasetKey"] as string ?? string.Empty,
                        ToDatasetKey = reader["ToDatasetKey"] as string ?? string.Empty,
                        JoinType = reader["JoinType"] as string ?? string.Empty,
                        JoinConditionTemplate = reader["JoinConditionTemplate"] as string ?? string.Empty,
                        IsEnabled = reader["IsEnabled"] != DBNull.Value && Convert.ToBoolean(reader["IsEnabled"])
                    });
                }

                return results;
            },
            results => JsonSerializer.Serialize(results, JsonOptions),
            json => JsonSerializer.Deserialize<List<EntityGraphCatalogEntry>>(json, JsonOptions),
            cancellationToken);
    }
}
