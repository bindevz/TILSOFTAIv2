using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Orchestration.Capabilities;

namespace TILSOFTAI.Infrastructure.Catalog;

public sealed class SqlPlatformCatalogMutationStore : IPlatformCatalogMutationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly SqlOptions _sqlOptions;

    public SqlPlatformCatalogMutationStore(IOptions<SqlOptions> sqlOptions)
    {
        _sqlOptions = sqlOptions?.Value ?? throw new ArgumentNullException(nameof(sqlOptions));
    }

    public async Task<IReadOnlyList<CapabilityDescriptor>> ListCapabilitiesAsync(CancellationToken ct)
    {
        var results = new List<CapabilityDescriptor>();
        await using var connection = await OpenAsync(ct);
        await using var command = CreateCommand(connection, "dbo.app_platform_capabilitycatalog_list");
        await using var reader = await command.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            results.Add(ReadCapability(reader));
        }

        return results;
    }

    public async Task<IReadOnlyList<KeyValuePair<string, ExternalConnectionOptions>>> ListExternalConnectionsAsync(CancellationToken ct)
    {
        var results = new List<KeyValuePair<string, ExternalConnectionOptions>>();
        await using var connection = await OpenAsync(ct);
        await using var command = CreateCommand(connection, "dbo.app_platform_externalconnectioncatalog_list");
        await using var reader = await command.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            results.Add(new KeyValuePair<string, ExternalConnectionOptions>(
                reader["ConnectionName"] as string ?? string.Empty,
                ReadConnection(reader)));
        }

        return results;
    }

    public async Task<IReadOnlyList<CatalogChangeRequestRecord>> ListChangesAsync(string tenantId, CancellationToken ct)
    {
        var results = new List<CatalogChangeRequestRecord>();
        await using var connection = await OpenAsync(ct);
        await using var command = CreateCommand(connection, "dbo.app_platform_catalogchange_list");
        command.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.NVarChar, 50) { Value = tenantId });
        await using var reader = await command.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            results.Add(ReadChange(reader));
        }

        return results;
    }

    public async Task<CatalogChangeRequestRecord?> GetChangeAsync(string tenantId, string changeId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = CreateCommand(connection, "dbo.app_platform_catalogchange_get");
        command.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.NVarChar, 50) { Value = tenantId });
        command.Parameters.Add(new SqlParameter("@ChangeId", SqlDbType.NVarChar, 64) { Value = changeId });
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadChange(reader) : null;
    }

    public async Task<CatalogRecordVersion> GetRecordVersionAsync(string recordType, string recordKey, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = CreateCommand(connection, "dbo.app_platform_catalogrecord_version");
        command.Parameters.Add(new SqlParameter("@RecordType", SqlDbType.NVarChar, 50) { Value = recordType });
        command.Parameters.Add(new SqlParameter("@RecordKey", SqlDbType.NVarChar, 200) { Value = recordKey });
        await using var reader = await command.ExecuteReaderAsync(ct);

        return await reader.ReadAsync(ct)
            ? new CatalogRecordVersion
            {
                Exists = Convert.ToBoolean(reader["RecordExists"]),
                VersionTag = reader["VersionTag"] as string ?? string.Empty
            }
            : new CatalogRecordVersion();
    }

    public async Task<CatalogChangeRequestRecord?> FindDuplicatePendingChangeAsync(
        string tenantId,
        string recordType,
        string operation,
        string recordKey,
        string payloadHash,
        string idempotencyKey,
        CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = CreateCommand(connection, "dbo.app_platform_catalogchange_find_duplicate");
        command.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.NVarChar, 50) { Value = tenantId });
        command.Parameters.Add(new SqlParameter("@RecordType", SqlDbType.NVarChar, 50) { Value = recordType });
        command.Parameters.Add(new SqlParameter("@Operation", SqlDbType.NVarChar, 50) { Value = operation });
        command.Parameters.Add(new SqlParameter("@RecordKey", SqlDbType.NVarChar, 200) { Value = recordKey });
        command.Parameters.Add(new SqlParameter("@PayloadHash", SqlDbType.NVarChar, 128) { Value = payloadHash });
        command.Parameters.Add(new SqlParameter("@IdempotencyKey", SqlDbType.NVarChar, 200) { Value = DbNullable(idempotencyKey) });
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadChange(reader) : null;
    }

    public async Task<CatalogChangeRequestRecord> CreateChangeAsync(CatalogChangeRequestRecord change, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = CreateCommand(connection, "dbo.app_platform_catalogchange_create");
        AddChangeParameters(command, change);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? ReadChange(reader)
            : throw new InvalidOperationException("Failed to create platform catalog change request.");
    }

    public Task<CatalogChangeRequestRecord> ApproveChangeAsync(string tenantId, string changeId, string reviewerUserId, CancellationToken ct) =>
        ReviewAsync("dbo.app_platform_catalogchange_approve", tenantId, changeId, reviewerUserId, ct);

    public Task<CatalogChangeRequestRecord> RejectChangeAsync(string tenantId, string changeId, string reviewerUserId, CancellationToken ct) =>
        ReviewAsync("dbo.app_platform_catalogchange_reject", tenantId, changeId, reviewerUserId, ct);

    public async Task<CatalogChangeRequestRecord> MarkAppliedAsync(string tenantId, string changeId, string appliedByUserId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = CreateCommand(connection, "dbo.app_platform_catalogchange_mark_applied");
        command.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.NVarChar, 50) { Value = tenantId });
        command.Parameters.Add(new SqlParameter("@ChangeId", SqlDbType.NVarChar, 64) { Value = changeId });
        command.Parameters.Add(new SqlParameter("@AppliedByUserId", SqlDbType.NVarChar, 200) { Value = appliedByUserId });
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? ReadChange(reader)
            : throw new InvalidOperationException("Failed to mark catalog change as applied.");
    }

    public async Task UpsertCapabilityAsync(CapabilityDescriptor capability, CatalogChangeRequestRecord change, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = CreateCommand(connection, "dbo.app_platform_capabilitycatalog_upsert");
        command.Parameters.Add(new SqlParameter("@CapabilityKey", SqlDbType.NVarChar, 200) { Value = capability.CapabilityKey });
        command.Parameters.Add(new SqlParameter("@Domain", SqlDbType.NVarChar, 100) { Value = capability.Domain });
        command.Parameters.Add(new SqlParameter("@AdapterType", SqlDbType.NVarChar, 100) { Value = capability.AdapterType });
        command.Parameters.Add(new SqlParameter("@Operation", SqlDbType.NVarChar, 100) { Value = capability.Operation });
        command.Parameters.Add(new SqlParameter("@TargetSystemId", SqlDbType.NVarChar, 200) { Value = capability.TargetSystemId });
        command.Parameters.Add(new SqlParameter("@ExecutionMode", SqlDbType.NVarChar, 50) { Value = capability.ExecutionMode });
        command.Parameters.Add(new SqlParameter("@RequiredRolesJson", SqlDbType.NVarChar, -1) { Value = JsonSerializer.Serialize(capability.RequiredRoles, JsonOptions) });
        command.Parameters.Add(new SqlParameter("@AllowedTenantsJson", SqlDbType.NVarChar, -1) { Value = JsonSerializer.Serialize(capability.AllowedTenants, JsonOptions) });
        command.Parameters.Add(new SqlParameter("@IntegrationBindingJson", SqlDbType.NVarChar, -1) { Value = JsonSerializer.Serialize(capability.IntegrationBinding, JsonOptions) });
        command.Parameters.Add(new SqlParameter("@ArgumentContractJson", SqlDbType.NVarChar, -1) { Value = JsonSerializer.Serialize(capability.ArgumentContract, JsonOptions) });
        AddMutationMetadata(command, change);
        await ExecuteMutationAsync(command, "Capability upsert failed due to a catalog version conflict.", ct);
    }

    public async Task DisableCapabilityAsync(string capabilityKey, CatalogChangeRequestRecord change, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = CreateCommand(connection, "dbo.app_platform_capabilitycatalog_disable");
        command.Parameters.Add(new SqlParameter("@CapabilityKey", SqlDbType.NVarChar, 200) { Value = capabilityKey });
        AddMutationMetadata(command, change);
        await ExecuteMutationAsync(command, "Capability disable failed due to a catalog version conflict.", ct);
    }

    public async Task UpsertExternalConnectionAsync(string connectionName, ExternalConnectionOptions connection, CatalogChangeRequestRecord change, CancellationToken ct)
    {
        await using var sqlConnection = await OpenAsync(ct);
        await using var command = CreateCommand(sqlConnection, "dbo.app_platform_externalconnectioncatalog_upsert");
        command.Parameters.Add(new SqlParameter("@ConnectionName", SqlDbType.NVarChar, 200) { Value = connectionName });
        command.Parameters.Add(new SqlParameter("@BaseUrl", SqlDbType.NVarChar, 1000) { Value = connection.BaseUrl });
        command.Parameters.Add(new SqlParameter("@AuthScheme", SqlDbType.NVarChar, 100) { Value = DbNullable(connection.AuthScheme) });
        command.Parameters.Add(new SqlParameter("@AuthTokenSecret", SqlDbType.NVarChar, 500) { Value = DbNullable(connection.AuthTokenSecret) });
        command.Parameters.Add(new SqlParameter("@ApiKeyHeader", SqlDbType.NVarChar, 200) { Value = DbNullable(connection.ApiKeyHeader) });
        command.Parameters.Add(new SqlParameter("@ApiKeySecret", SqlDbType.NVarChar, 500) { Value = DbNullable(connection.ApiKeySecret) });
        command.Parameters.Add(new SqlParameter("@TimeoutSeconds", SqlDbType.Int) { Value = connection.TimeoutSeconds });
        command.Parameters.Add(new SqlParameter("@RetryCount", SqlDbType.Int) { Value = connection.RetryCount });
        command.Parameters.Add(new SqlParameter("@RetryDelayMs", SqlDbType.Int) { Value = connection.RetryDelayMs });
        command.Parameters.Add(new SqlParameter("@HeadersJson", SqlDbType.NVarChar, -1) { Value = JsonSerializer.Serialize(connection.Headers, JsonOptions) });
        command.Parameters.Add(new SqlParameter("@HeaderSecretsJson", SqlDbType.NVarChar, -1) { Value = JsonSerializer.Serialize(connection.HeaderSecrets, JsonOptions) });
        AddMutationMetadata(command, change);
        await ExecuteMutationAsync(command, "External connection upsert failed due to a catalog version conflict.", ct);
    }

    public async Task DisableExternalConnectionAsync(string connectionName, CatalogChangeRequestRecord change, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = CreateCommand(connection, "dbo.app_platform_externalconnectioncatalog_disable");
        command.Parameters.Add(new SqlParameter("@ConnectionName", SqlDbType.NVarChar, 200) { Value = connectionName });
        AddMutationMetadata(command, change);
        await ExecuteMutationAsync(command, "External connection disable failed due to a catalog version conflict.", ct);
    }

    private async Task<CatalogChangeRequestRecord> ReviewAsync(
        string storedProcedure,
        string tenantId,
        string changeId,
        string reviewerUserId,
        CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = CreateCommand(connection, storedProcedure);
        command.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.NVarChar, 50) { Value = tenantId });
        command.Parameters.Add(new SqlParameter("@ChangeId", SqlDbType.NVarChar, 64) { Value = changeId });
        command.Parameters.Add(new SqlParameter("@ReviewedByUserId", SqlDbType.NVarChar, 200) { Value = reviewerUserId });
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? ReadChange(reader)
            : throw new InvalidOperationException("Catalog change review failed.");
    }

    private async Task<SqlConnection> OpenAsync(CancellationToken ct)
    {
        var connection = new SqlConnection(_sqlOptions.ConnectionString);
        await connection.OpenAsync(ct);
        return connection;
    }

    private SqlCommand CreateCommand(SqlConnection connection, string storedProcedure) => new(storedProcedure, connection)
    {
        CommandType = CommandType.StoredProcedure,
        CommandTimeout = _sqlOptions.CommandTimeoutSeconds
    };

    private static void AddChangeParameters(SqlCommand command, CatalogChangeRequestRecord change)
    {
        command.Parameters.Add(new SqlParameter("@ChangeId", SqlDbType.NVarChar, 64) { Value = change.ChangeId });
        command.Parameters.Add(new SqlParameter("@TenantId", SqlDbType.NVarChar, 50) { Value = change.TenantId });
        command.Parameters.Add(new SqlParameter("@RecordType", SqlDbType.NVarChar, 50) { Value = change.RecordType });
        command.Parameters.Add(new SqlParameter("@Operation", SqlDbType.NVarChar, 50) { Value = change.Operation });
        command.Parameters.Add(new SqlParameter("@RecordKey", SqlDbType.NVarChar, 200) { Value = change.RecordKey });
        command.Parameters.Add(new SqlParameter("@PayloadJson", SqlDbType.NVarChar, -1) { Value = change.PayloadJson });
        command.Parameters.Add(new SqlParameter("@Owner", SqlDbType.NVarChar, 200) { Value = change.Owner });
        command.Parameters.Add(new SqlParameter("@ChangeNote", SqlDbType.NVarChar, 1000) { Value = change.ChangeNote });
        command.Parameters.Add(new SqlParameter("@VersionTag", SqlDbType.NVarChar, 100) { Value = DbNullable(change.VersionTag) });
        command.Parameters.Add(new SqlParameter("@ExpectedVersionTag", SqlDbType.NVarChar, 100) { Value = DbNullable(change.ExpectedVersionTag) });
        command.Parameters.Add(new SqlParameter("@IdempotencyKey", SqlDbType.NVarChar, 200) { Value = DbNullable(change.IdempotencyKey) });
        command.Parameters.Add(new SqlParameter("@RollbackOfChangeId", SqlDbType.NVarChar, 64) { Value = DbNullable(change.RollbackOfChangeId) });
        command.Parameters.Add(new SqlParameter("@PayloadHash", SqlDbType.NVarChar, 128) { Value = change.PayloadHash });
        command.Parameters.Add(new SqlParameter("@RiskLevel", SqlDbType.NVarChar, 50) { Value = change.RiskLevel });
        command.Parameters.Add(new SqlParameter("@EnvironmentName", SqlDbType.NVarChar, 100) { Value = change.EnvironmentName });
        command.Parameters.Add(new SqlParameter("@BreakGlass", SqlDbType.Bit) { Value = change.BreakGlass });
        command.Parameters.Add(new SqlParameter("@BreakGlassJustification", SqlDbType.NVarChar, 1000) { Value = DbNullable(change.BreakGlassJustification) });
        command.Parameters.Add(new SqlParameter("@RequestedByUserId", SqlDbType.NVarChar, 200) { Value = change.RequestedByUserId });
    }

    private static void AddMutationMetadata(SqlCommand command, CatalogChangeRequestRecord change)
    {
        command.Parameters.Add(new SqlParameter("@VersionTag", SqlDbType.NVarChar, 100) { Value = DbNullable(change.VersionTag) });
        command.Parameters.Add(new SqlParameter("@ExpectedVersionTag", SqlDbType.NVarChar, 100) { Value = DbNullable(change.ExpectedVersionTag) });
        command.Parameters.Add(new SqlParameter("@UpdatedBy", SqlDbType.NVarChar, 200) { Value = change.AppliedByUserId ?? change.ReviewedByUserId ?? change.RequestedByUserId });
    }

    private static async Task ExecuteMutationAsync(SqlCommand command, string conflictMessage, CancellationToken ct)
    {
        var affected = await command.ExecuteNonQueryAsync(ct);
        if (affected == 0)
        {
            throw new InvalidOperationException(conflictMessage);
        }
    }

    private static CapabilityDescriptor ReadCapability(SqlDataReader reader) => new()
    {
        CapabilityKey = reader["CapabilityKey"] as string ?? string.Empty,
        Domain = reader["Domain"] as string ?? string.Empty,
        AdapterType = reader["AdapterType"] as string ?? string.Empty,
        Operation = reader["Operation"] as string ?? string.Empty,
        TargetSystemId = reader["TargetSystemId"] as string ?? string.Empty,
        ExecutionMode = reader["ExecutionMode"] as string ?? "readonly",
        RequiredRoles = DeserializeList(reader["RequiredRolesJson"] as string),
        AllowedTenants = DeserializeList(reader["AllowedTenantsJson"] as string),
        IntegrationBinding = DeserializeDictionary(reader["IntegrationBindingJson"] as string),
        ArgumentContract = DeserializeContract(reader["ArgumentContractJson"] as string)
    };

    private static ExternalConnectionOptions ReadConnection(SqlDataReader reader) => new()
    {
        BaseUrl = reader["BaseUrl"] as string ?? string.Empty,
        AuthScheme = reader["AuthScheme"] as string ?? string.Empty,
        AuthTokenSecret = reader["AuthTokenSecret"] as string ?? string.Empty,
        ApiKeyHeader = reader["ApiKeyHeader"] as string ?? string.Empty,
        ApiKeySecret = reader["ApiKeySecret"] as string ?? string.Empty,
        TimeoutSeconds = reader["TimeoutSeconds"] != DBNull.Value ? Convert.ToInt32(reader["TimeoutSeconds"]) : 0,
        RetryCount = reader["RetryCount"] != DBNull.Value ? Convert.ToInt32(reader["RetryCount"]) : 0,
        RetryDelayMs = reader["RetryDelayMs"] != DBNull.Value ? Convert.ToInt32(reader["RetryDelayMs"]) : 0,
        Headers = DeserializeDictionary(reader["HeadersJson"] as string),
        HeaderSecrets = DeserializeDictionary(reader["HeaderSecretsJson"] as string)
    };

    private static CatalogChangeRequestRecord ReadChange(SqlDataReader reader) => new()
    {
        ChangeId = reader["ChangeId"] as string ?? string.Empty,
        TenantId = reader["TenantId"] as string ?? string.Empty,
        RecordType = reader["RecordType"] as string ?? string.Empty,
        Operation = reader["Operation"] as string ?? string.Empty,
        RecordKey = reader["RecordKey"] as string ?? string.Empty,
        PayloadJson = reader["PayloadJson"] as string ?? "{}",
        Status = reader["Status"] as string ?? string.Empty,
        Owner = reader["Owner"] as string ?? string.Empty,
        ChangeNote = reader["ChangeNote"] as string ?? string.Empty,
        VersionTag = reader["VersionTag"] as string ?? string.Empty,
        ExpectedVersionTag = reader["ExpectedVersionTag"] as string ?? string.Empty,
        IdempotencyKey = reader["IdempotencyKey"] as string ?? string.Empty,
        RollbackOfChangeId = reader["RollbackOfChangeId"] as string ?? string.Empty,
        PayloadHash = reader["PayloadHash"] as string ?? string.Empty,
        RiskLevel = reader["RiskLevel"] as string ?? CatalogChangeRiskLevels.Standard,
        EnvironmentName = reader["EnvironmentName"] as string ?? string.Empty,
        BreakGlass = reader["BreakGlass"] != DBNull.Value && Convert.ToBoolean(reader["BreakGlass"]),
        BreakGlassJustification = reader["BreakGlassJustification"] as string ?? string.Empty,
        RequestedByUserId = reader["RequestedByUserId"] as string ?? string.Empty,
        RequestedAtUtc = reader["RequestedAtUtc"] != DBNull.Value ? (DateTime)reader["RequestedAtUtc"] : DateTime.MinValue,
        ReviewedByUserId = reader["ReviewedByUserId"] as string,
        ReviewedAtUtc = reader["ReviewedAtUtc"] != DBNull.Value ? (DateTime?)reader["ReviewedAtUtc"] : null,
        AppliedByUserId = reader["AppliedByUserId"] as string,
        AppliedAtUtc = reader["AppliedAtUtc"] != DBNull.Value ? (DateTime?)reader["AppliedAtUtc"] : null
    };

    private static IReadOnlyList<string> DeserializeList(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? Array.Empty<string>()
            : JsonSerializer.Deserialize<string[]>(json, JsonOptions) ?? Array.Empty<string>();

    private static Dictionary<string, string> DeserializeDictionary(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static CapabilityArgumentContract? DeserializeContract(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<CapabilityArgumentContract>(json, JsonOptions);

    private static object DbNullable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
}
