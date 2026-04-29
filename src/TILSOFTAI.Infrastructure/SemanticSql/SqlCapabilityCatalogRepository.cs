using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Orchestration.Capabilities;
using TILSOFTAI.Orchestration.Semantic;

namespace TILSOFTAI.Infrastructure.SemanticSql;

public sealed class SqlCapabilityCatalogRepository :
    ISqlBackedCapabilityCatalog,
    ICapabilityRegistry,
    ICapabilityMetadataRepository,
    ICapabilityCatalogReloader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly SqlOptions _sqlOptions;
    private readonly ILogger<SqlCapabilityCatalogRepository> _logger;
    private readonly SemaphoreSlim _reloadLock = new(1, 1);
    private readonly TimeSpan _ttl = TimeSpan.FromMinutes(5);
    private CatalogSnapshot? _snapshot;

    public SqlCapabilityCatalogRepository(
        IOptions<SqlOptions> sqlOptions,
        ILogger<SqlCapabilityCatalogRepository>? logger = null)
    {
        _sqlOptions = sqlOptions?.Value ?? throw new ArgumentNullException(nameof(sqlOptions));
        _logger = logger ?? NullLogger<SqlCapabilityCatalogRepository>.Instance;
    }

    public async Task<IReadOnlyList<CapabilityDescriptor>> GetEnabledCapabilitiesAsync(
        IReadOnlySet<string> allowedDomains,
        CancellationToken ct)
    {
        var snapshot = await EnsureSnapshotAsync(ct).ConfigureAwait(false);
        return snapshot.Descriptors
            .Where(capability => allowedDomains.Count == 0 || allowedDomains.Contains(capability.Domain))
            .ToArray();
    }

    public async Task<CapabilityDescriptor?> GetByKeyAsync(string capabilityKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(capabilityKey))
        {
            throw new ArgumentException("Capability key is required.", nameof(capabilityKey));
        }

        var snapshot = await EnsureSnapshotAsync(ct).ConfigureAwait(false);
        return snapshot.ByKey.TryGetValue(capabilityKey, out var capability) ? capability.Descriptor : null;
    }

    public async Task<IReadOnlyList<CapabilityDescriptor>> GetByDomainAsync(string domain, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return Array.Empty<CapabilityDescriptor>();
        }

        var snapshot = await EnsureSnapshotAsync(ct).ConfigureAwait(false);
        return snapshot.Descriptors
            .Where(capability => capability.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    public async Task<CapabilitySemanticMetadata?> GetCapabilityMetadataAsync(
        string capabilityKey,
        string locale,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(capabilityKey))
        {
            throw new ArgumentException("Capability key is required.", nameof(capabilityKey));
        }

        var metadata = await LoadCapabilityMetadataAsync(capabilityKey, NormalizeLocale(locale), cancellationToken)
            .ConfigureAwait(false);
        return metadata;
    }

    public IReadOnlyList<CapabilityDescriptor> GetAll() =>
        EnsureSnapshotAsync(CancellationToken.None).GetAwaiter().GetResult().Descriptors;

    public IReadOnlyList<CapabilityDescriptor> GetByDomain(string domain) =>
        GetByDomainAsync(domain, CancellationToken.None).GetAwaiter().GetResult();

    public CapabilityDescriptor? Resolve(string capabilityKey) =>
        GetByKeyAsync(capabilityKey, CancellationToken.None).GetAwaiter().GetResult();

    public async Task ReloadAsync(CancellationToken ct)
    {
        await _reloadLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _snapshot = await LoadSnapshotAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    private async Task<CatalogSnapshot> EnsureSnapshotAsync(CancellationToken ct)
    {
        var current = _snapshot;
        if (current is not null && DateTimeOffset.UtcNow - current.LoadedAtUtc < _ttl)
        {
            return current;
        }

        await _reloadLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            current = _snapshot;
            if (current is not null && DateTimeOffset.UtcNow - current.LoadedAtUtc < _ttl)
            {
                return current;
            }

            try
            {
                _snapshot = await LoadSnapshotAsync(ct).ConfigureAwait(false);
                return _snapshot;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (current is not null)
                {
                    _logger.LogWarning(ex, "SQL capability catalog reload failed; using last known good catalog.");
                    return current;
                }

                _logger.LogError(ex, "SQL capability catalog initial load failed closed.");
                throw;
            }
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    private async Task<CatalogSnapshot> LoadSnapshotAsync(CancellationToken ct)
    {
        var metadatas = await LoadEnabledMetadataAsync("vi-VN", ct).ConfigureAwait(false);
        if (metadatas.Count == 0)
        {
            throw new InvalidOperationException("SQL capability catalog returned no enabled capabilities.");
        }

        var entries = metadatas
            .Select(metadata => new CatalogEntry(metadata.CapabilityKey, metadata, ToDescriptor(metadata)))
            .ToArray();

        _logger.LogInformation(
            "Loaded {CapabilityCount} SQL-backed capability descriptors from catalog.",
            entries.Length);

        return new CatalogSnapshot(
            DateTimeOffset.UtcNow,
            entries.Select(entry => entry.Descriptor).ToArray(),
            entries.ToDictionary(entry => entry.CapabilityKey, StringComparer.OrdinalIgnoreCase));
    }

    private async Task<IReadOnlyList<CapabilitySemanticMetadata>> LoadEnabledMetadataAsync(
        string locale,
        CancellationToken ct)
    {
        var results = new List<CapabilitySemanticMetadata>();
        await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using var command = CreateBaseMetadataCommand(connection, locale);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(ReadMetadata(reader));
        }

        for (var i = 0; i < results.Count; i++)
        {
            var capabilityKey = results[i].CapabilityKey;
            var arguments = await LoadArgumentsAsync(connection, capabilityKey, locale, ct).ConfigureAwait(false);
            var examples = await LoadExamplesAsync(connection, capabilityKey, locale, ct).ConfigureAwait(false);
            results[i] = results[i] with
            {
                Arguments = arguments,
                Examples = examples
            };
        }

        return results;
    }

    private async Task<CapabilitySemanticMetadata?> LoadCapabilityMetadataAsync(
        string capabilityKey,
        string locale,
        CancellationToken ct)
    {
        await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using var command = CreateBaseMetadataCommand(connection, locale);
        command.CommandText += " AND c.CapabilityKey = @CapabilityKey";
        command.Parameters.Add(new SqlParameter("@CapabilityKey", SqlDbType.NVarChar, 200) { Value = capabilityKey });

        CapabilitySemanticMetadata? metadata = null;
        await using (var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            if (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                metadata = ReadMetadata(reader);
            }
        }

        if (metadata is null)
        {
            return null;
        }

        var arguments = await LoadArgumentsAsync(connection, metadata.CapabilityKey, locale, ct).ConfigureAwait(false);
        var examples = await LoadExamplesAsync(connection, metadata.CapabilityKey, locale, ct).ConfigureAwait(false);
        return metadata with
        {
            Arguments = arguments,
            Examples = examples
        };
    }

    private SqlCommand CreateBaseMetadataCommand(SqlConnection connection, string locale)
    {
        var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandTimeout = _sqlOptions.CommandTimeoutSeconds;
        command.CommandText = @"
SELECT
    c.CapabilityKey,
    c.Domain,
    c.BusinessArea,
    c.FunctionName,
    c.AdapterType,
    c.Operation,
    COALESCE(c.TargetSystemId, c.AdapterType) AS TargetSystemId,
    COALESCE(c.StoredProcedureName, c.StoredProcedure) AS StoredProcedure,
    c.ExecutionMode,
    c.ArgumentContract,
    COALESCE(rs.SchemaJson, c.ResultSchema) AS ResultSchema,
    COALESCE(ap.PolicyJson, c.AnswerPolicy) AS AnswerPolicy,
    COALESCE(sp.PolicyJson, c.SensitivityPolicy) AS SensitivityPolicy,
    c.RequiredRoles,
    c.AllowedTenants,
    c.AllowMultiCall,
    COALESCE(c.Version, c.VersionNo) AS VersionNo,
    t.Locale,
    COALESCE(t.Name, t.ShortName) AS ShortName,
    t.Description,
    t.NegativeDescription,
    t.AliasesJson,
    t.BusinessNotes
FROM ai.Capability c
LEFT JOIN ai.CapabilityResultSchema rs ON rs.CapabilityKey = c.CapabilityKey
LEFT JOIN ai.CapabilityAnswerPolicy ap ON ap.CapabilityKey = c.CapabilityKey
LEFT JOIN ai.CapabilitySensitivityPolicy sp ON sp.CapabilityKey = c.CapabilityKey
OUTER APPLY (
    SELECT TOP (1) text.*
    FROM ai.CapabilityText text
    WHERE text.CapabilityKey = c.CapabilityKey
      AND (text.Locale = @Locale OR text.Locale = N'en-US' OR text.IsDefault = 1)
    ORDER BY CASE
        WHEN text.Locale = @Locale THEN 0
        WHEN text.Locale = N'en-US' THEN 1
        WHEN text.IsDefault = 1 THEN 2
        ELSE 3
    END
) t
WHERE c.IsEnabled = 1
  AND c.IsActive = 1";
        command.Parameters.Add(new SqlParameter("@Locale", SqlDbType.NVarChar, 20) { Value = NormalizeLocale(locale) });
        return command;
    }

    private async Task<IReadOnlyList<CapabilityArgumentMetadata>> LoadArgumentsAsync(
        SqlConnection connection,
        string capabilityKey,
        string locale,
        CancellationToken ct)
    {
        var arguments = new List<CapabilityArgumentMetadata>();
        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandTimeout = _sqlOptions.CommandTimeoutSeconds;
        command.CommandText = @"
SELECT
    a.ArgumentName,
    COALESCE(a.ProcParameterName, a.ModelFacingName, a.ArgumentName) AS ProcParameterName,
    COALESCE(a.Type, a.DataType, N'string') AS DataType,
    a.IsRequired,
    a.DefaultSource,
    COALESCE(a.ValidationRule,
        JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(N'{}', N'$.format', a.Format), N'$.pattern', a.RegexPattern), N'$.minLength', a.MinLength), N'$.maxLength', a.MaxLength)) AS ValidationRule,
    a.ClarificationPolicy,
    COALESCE(a.SortOrder, a.DisplayOrder, 0) AS DisplayOrder,
    t.Locale,
    t.Description,
    t.AliasesJson,
    t.ExamplesJson,
    t.ClarificationQuestion
FROM ai.CapabilityArgument a
OUTER APPLY (
    SELECT TOP (1) text.*
    FROM ai.CapabilityArgumentText text
    WHERE text.CapabilityKey = a.CapabilityKey
      AND text.ArgumentName = a.ArgumentName
      AND (text.Locale = @Locale OR text.Locale = N'en-US')
    ORDER BY CASE WHEN text.Locale = @Locale THEN 0 WHEN text.Locale = N'en-US' THEN 1 ELSE 2 END
) t
WHERE a.CapabilityKey = @CapabilityKey
  AND a.IsActive = 1
ORDER BY COALESCE(a.SortOrder, a.DisplayOrder, 0), a.ArgumentName;";

        command.Parameters.Add(new SqlParameter("@CapabilityKey", SqlDbType.NVarChar, 200) { Value = capabilityKey });
        command.Parameters.Add(new SqlParameter("@Locale", SqlDbType.NVarChar, 20) { Value = NormalizeLocale(locale) });

        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            arguments.Add(new CapabilityArgumentMetadata
            {
                ArgumentName = ReadString(reader, "ArgumentName") ?? string.Empty,
                ProcParameterName = ReadString(reader, "ProcParameterName") ?? string.Empty,
                DataType = ReadString(reader, "DataType") ?? "string",
                IsRequired = ReadBoolean(reader, "IsRequired"),
                DefaultSource = ReadString(reader, "DefaultSource"),
                ValidationRule = ReadString(reader, "ValidationRule"),
                ClarificationPolicy = ReadString(reader, "ClarificationPolicy"),
                DisplayOrder = ReadInt32(reader, "DisplayOrder"),
                Text = ReadString(reader, "Description") is { } description
                    ? new CapabilityArgumentTextMetadata
                    {
                        Locale = ReadString(reader, "Locale") ?? NormalizeLocale(locale),
                        Description = description,
                        Aliases = ReadString(reader, "AliasesJson"),
                        Examples = ReadString(reader, "ExamplesJson"),
                        ClarificationQuestion = ReadString(reader, "ClarificationQuestion")
                    }
                    : null
            });
        }

        return arguments;
    }

    private async Task<IReadOnlyList<CapabilityExampleMetadata>> LoadExamplesAsync(
        SqlConnection connection,
        string capabilityKey,
        string locale,
        CancellationToken ct)
    {
        var examples = new List<CapabilityExampleMetadata>();
        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandTimeout = _sqlOptions.CommandTimeoutSeconds;
        command.CommandText = @"
SELECT Locale, Utterance, ArgumentsJson, SortOrder
FROM ai.CapabilityExample
WHERE CapabilityKey = @CapabilityKey
  AND (Locale = @Locale OR Locale = N'en-US')
ORDER BY CASE WHEN Locale = @Locale THEN 0 WHEN Locale = N'en-US' THEN 1 ELSE 2 END, SortOrder;";

        command.Parameters.Add(new SqlParameter("@CapabilityKey", SqlDbType.NVarChar, 200) { Value = capabilityKey });
        command.Parameters.Add(new SqlParameter("@Locale", SqlDbType.NVarChar, 20) { Value = NormalizeLocale(locale) });

        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            examples.Add(new CapabilityExampleMetadata
            {
                Locale = ReadString(reader, "Locale") ?? NormalizeLocale(locale),
                Utterance = ReadString(reader, "Utterance") ?? string.Empty,
                ArgumentsJson = ReadString(reader, "ArgumentsJson"),
                SortOrder = ReadInt32(reader, "SortOrder")
            });
        }

        return examples;
    }

    private static CapabilitySemanticMetadata ReadMetadata(SqlDataReader reader) => new()
    {
        CapabilityKey = ReadString(reader, "CapabilityKey") ?? string.Empty,
        Domain = ReadString(reader, "Domain") ?? string.Empty,
        BusinessArea = ReadString(reader, "BusinessArea"),
        FunctionName = ReadString(reader, "FunctionName") ?? string.Empty,
        AdapterType = ReadString(reader, "AdapterType") ?? string.Empty,
        Operation = ReadString(reader, "Operation") ?? string.Empty,
        StoredProcedure = ReadString(reader, "StoredProcedure"),
        ExecutionMode = ReadString(reader, "ExecutionMode") ?? string.Empty,
        ArgumentContract = ReadString(reader, "ArgumentContract"),
        ResultSchema = ReadString(reader, "ResultSchema"),
        AnswerPolicy = ReadString(reader, "AnswerPolicy"),
        SensitivityPolicy = ReadString(reader, "SensitivityPolicy"),
        RequiredRoles = ReadString(reader, "RequiredRoles"),
        AllowedTenants = ReadString(reader, "AllowedTenants"),
        AllowMultiCall = ReadBoolean(reader, "AllowMultiCall"),
        VersionNo = ReadInt32(reader, "VersionNo"),
        Text = ReadString(reader, "Description") is { } description
            ? new CapabilityTextMetadata
            {
                Locale = ReadString(reader, "Locale") ?? "vi-VN",
                ShortName = ReadString(reader, "ShortName"),
                Description = description,
                Aliases = ReadString(reader, "AliasesJson"),
                DoNotUseWhen = ReadString(reader, "NegativeDescription"),
                BusinessNotes = ReadString(reader, "BusinessNotes")
            }
            : null
    };

    private static CapabilityDescriptor ToDescriptor(CapabilitySemanticMetadata metadata)
    {
        var binding = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(metadata.StoredProcedure))
        {
            binding["storedProcedure"] = metadata.StoredProcedure;
        }

        return new CapabilityDescriptor
        {
            CapabilityKey = metadata.CapabilityKey,
            Domain = metadata.Domain,
            AdapterType = metadata.AdapterType,
            Operation = metadata.Operation,
            TargetSystemId = metadata.AdapterType.Equals("sql", StringComparison.OrdinalIgnoreCase)
                ? "sql"
                : metadata.AdapterType,
            IntegrationBinding = binding,
            RequiredRoles = ReadStringList(metadata.RequiredRoles),
            AllowedTenants = ReadStringList(metadata.AllowedTenants),
            ArgumentContract = BuildArgumentContract(metadata),
            ExecutionMode = NormalizeExecutionMode(metadata.ExecutionMode)
        };
    }

    private static CapabilityArgumentContract BuildArgumentContract(CapabilitySemanticMetadata metadata)
    {
        if (!string.IsNullOrWhiteSpace(metadata.ArgumentContract))
        {
            var parsed = JsonSerializer.Deserialize<CapabilityArgumentContract>(metadata.ArgumentContract, JsonOptions);
            if (parsed is not null)
            {
                return parsed;
            }
        }

        return new CapabilityArgumentContract
        {
            RequiredArguments = metadata.Arguments
                .Where(argument => argument.IsRequired)
                .Select(argument => argument.ProcParameterName)
                .ToArray(),
            AllowedArguments = metadata.Arguments
                .Select(argument => argument.ProcParameterName)
                .ToArray(),
            AllowAdditionalArguments = false,
            Arguments = metadata.Arguments.Select(ToArgumentRule).ToArray()
        };
    }

    private static CapabilityArgumentRule ToArgumentRule(CapabilityArgumentMetadata argument)
    {
        var validation = ReadValidationRule(argument.ValidationRule);
        return new CapabilityArgumentRule
        {
            Name = argument.ProcParameterName,
            Type = NormalizeArgumentType(argument.DataType),
            Format = validation.Format,
            Enum = validation.Enum,
            Min = validation.Min,
            Max = validation.Max,
            MinLength = validation.MinLength,
            MaxLength = validation.MaxLength,
            Pattern = validation.Pattern
        };
    }

    private static CapabilityArgumentRule ReadValidationRule(string? validationRule)
    {
        if (string.IsNullOrWhiteSpace(validationRule))
        {
            return new CapabilityArgumentRule();
        }

        try
        {
            return JsonSerializer.Deserialize<CapabilityArgumentRule>(validationRule, JsonOptions)
                ?? new CapabilityArgumentRule();
        }
        catch (JsonException)
        {
            return new CapabilityArgumentRule();
        }
    }

    private static IReadOnlyList<string> ReadStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(json, JsonOptions) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return json.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }

    private static string NormalizeExecutionMode(string executionMode) =>
        executionMode.Trim().ToLowerInvariant() switch
        {
            "read" or "read_only" => "readonly",
            _ => executionMode
        };

    private static string NormalizeArgumentType(string dataType) =>
        dataType.Trim().ToLowerInvariant() switch
        {
            "int" or "bigint" or "smallint" => "integer",
            "decimal" or "numeric" or "money" or "float" => "number",
            "bit" or "bool" => "boolean",
            _ => dataType.Trim().ToLowerInvariant()
        };

    private static string NormalizeLocale(string? locale) =>
        string.IsNullOrWhiteSpace(locale) ? "vi-VN" : locale;

    private static string? ReadString(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static bool ReadBoolean(SqlDataReader reader, string name) =>
        !reader.IsDBNull(reader.GetOrdinal(name)) && reader.GetBoolean(reader.GetOrdinal(name));

    private static int ReadInt32(SqlDataReader reader, string name) =>
        reader.IsDBNull(reader.GetOrdinal(name)) ? 0 : reader.GetInt32(reader.GetOrdinal(name));

    private sealed record CatalogEntry(
        string CapabilityKey,
        CapabilitySemanticMetadata Metadata,
        CapabilityDescriptor Descriptor);

    private sealed record CatalogSnapshot(
        DateTimeOffset LoadedAtUtc,
        IReadOnlyList<CapabilityDescriptor> Descriptors,
        IReadOnlyDictionary<string, CatalogEntry> ByKey);
}
