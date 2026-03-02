using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Orchestration.Modules;

namespace TILSOFTAI.Infrastructure.Modules;

/// <summary>
/// PATCH 37.01: SQL-backed module activation provider with IMemoryCache.
/// Calls app_module_runtime_list and caches results.
/// </summary>
public sealed class SqlModuleActivationProvider : IModuleActivationProvider
{
    private readonly SqlOptions _sqlOptions;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SqlModuleActivationProvider> _logger;

    private const string CachePrefix = "module_activation";
    private const int DefaultCacheTtlSeconds = 300;

    public SqlModuleActivationProvider(
        IOptions<SqlOptions> sqlOptions,
        IMemoryCache cache,
        ILogger<SqlModuleActivationProvider> logger)
    {
        _sqlOptions = sqlOptions?.Value ?? throw new ArgumentNullException(nameof(sqlOptions));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<string>> GetEnabledModulesAsync(
        string? tenantId = null,
        string? environment = null,
        CancellationToken ct = default)
    {
        var cacheKey = $"{CachePrefix}:{tenantId ?? "global"}:{environment ?? "any"}";

        if (_cache.TryGetValue<IReadOnlyList<string>>(cacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
            await connection.OpenAsync(ct);

            await using var command = new SqlCommand("dbo.app_module_runtime_list", connection)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = _sqlOptions.CommandTimeoutSeconds
            };

            command.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.NVarChar, 50)
            {
                Value = (object?)tenantId ?? DBNull.Value
            });
            command.Parameters.Add(new SqlParameter("@Environment", SqlDbType.NVarChar, 50)
            {
                Value = (object?)environment ?? DBNull.Value
            });

            var modules = new List<string>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var assemblyName = reader["AssemblyName"] as string;
                if (!string.IsNullOrWhiteSpace(assemblyName))
                {
                    modules.Add(assemblyName);
                }
            }

            _cache.Set(cacheKey, (IReadOnlyList<string>)modules,
                TimeSpan.FromSeconds(DefaultCacheTtlSeconds));

            _logger.LogInformation(
                "ModuleActivationResolved | TenantId: {TenantId} | Env: {Environment} | Modules: [{Modules}]",
                tenantId ?? "global", environment ?? "any", string.Join(", ", modules));

            return modules;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load modules from DB. Returning empty list for fallback.");
            return Array.Empty<string>();
        }
    }
}
