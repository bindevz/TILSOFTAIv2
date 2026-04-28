using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Orchestration.Semantic;

namespace TILSOFTAI.Infrastructure.SemanticSql;

public sealed class SqlCapabilityMetadataRepository : ICapabilityMetadataRepository
{
    private readonly SqlOptions _sqlOptions;

    public SqlCapabilityMetadataRepository(IOptions<SqlOptions> sqlOptions)
    {
        _sqlOptions = sqlOptions?.Value ?? throw new ArgumentNullException(nameof(sqlOptions));
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

        await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        CapabilitySemanticMetadata? capability = null;
        await using (var command = connection.CreateCommand())
        {
            command.CommandType = CommandType.Text;
            command.CommandTimeout = _sqlOptions.CommandTimeoutSeconds;
            command.CommandText = @"
SELECT TOP (1)
    c.CapabilityKey,
    c.Domain,
    c.BusinessArea,
    c.FunctionName,
    c.AdapterType,
    c.Operation,
    c.StoredProcedure,
    c.ExecutionMode,
    c.ArgumentContract,
    c.ResultSchema,
    c.AnswerPolicy,
    c.SensitivityPolicy,
    c.RequiredRoles,
    c.AllowedTenants,
    c.AllowMultiCall,
    c.VersionNo,
    t.Locale,
    t.ShortName,
    t.Description,
    t.UseWhen,
    t.DoNotUseWhen,
    t.BusinessNotes
FROM ai.Capability c
OUTER APPLY (
    SELECT TOP (1) *
    FROM ai.CapabilityText text
    WHERE text.CapabilityKey = c.CapabilityKey
      AND (text.Locale = @Locale OR text.Locale = @DefaultLocale OR text.Locale = N'en-US')
    ORDER BY CASE
        WHEN text.Locale = @Locale THEN 0
        WHEN text.Locale = @DefaultLocale THEN 1
        WHEN text.Locale = N'en-US' THEN 2
        ELSE 3
    END
) t
WHERE c.CapabilityKey = @CapabilityKey
  AND c.IsActive = 1;";

            command.Parameters.Add(new SqlParameter("@CapabilityKey", SqlDbType.NVarChar, 200) { Value = capabilityKey });
            command.Parameters.Add(new SqlParameter("@Locale", SqlDbType.NVarChar, 20) { Value = NormalizeLocale(locale) });
            command.Parameters.Add(new SqlParameter("@DefaultLocale", SqlDbType.NVarChar, 20) { Value = "vi-VN" });

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                capability = new CapabilitySemanticMetadata
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
                            Locale = ReadString(reader, "Locale") ?? NormalizeLocale(locale),
                            ShortName = ReadString(reader, "ShortName"),
                            Description = description,
                            UseWhen = ReadString(reader, "UseWhen"),
                            DoNotUseWhen = ReadString(reader, "DoNotUseWhen"),
                            BusinessNotes = ReadString(reader, "BusinessNotes")
                        }
                        : null
                };
            }
        }

        if (capability is null)
        {
            return null;
        }

        var arguments = await LoadArgumentsAsync(connection, capability.CapabilityKey, locale, cancellationToken);
        return capability with { Arguments = arguments };
    }

    private async Task<IReadOnlyList<CapabilityArgumentMetadata>> LoadArgumentsAsync(
        SqlConnection connection,
        string capabilityKey,
        string locale,
        CancellationToken cancellationToken)
    {
        var arguments = new List<CapabilityArgumentMetadata>();
        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandTimeout = _sqlOptions.CommandTimeoutSeconds;
        command.CommandText = @"
SELECT
    a.ArgumentName,
    a.ProcParameterName,
    a.DataType,
    a.IsRequired,
    a.DefaultSource,
    a.ValidationRule,
    a.ClarificationPolicy,
    a.DisplayOrder,
    t.Locale,
    t.Description,
    t.Aliases,
    t.Examples
FROM ai.CapabilityArgument a
OUTER APPLY (
    SELECT TOP (1) *
    FROM ai.ArgumentText text
    WHERE text.CapabilityKey = a.CapabilityKey
      AND text.ArgumentName = a.ArgumentName
      AND (text.Locale = @Locale OR text.Locale = @DefaultLocale OR text.Locale = N'en-US')
    ORDER BY CASE
        WHEN text.Locale = @Locale THEN 0
        WHEN text.Locale = @DefaultLocale THEN 1
        WHEN text.Locale = N'en-US' THEN 2
        ELSE 3
    END
) t
WHERE a.CapabilityKey = @CapabilityKey
  AND a.IsActive = 1
ORDER BY a.DisplayOrder, a.ArgumentName;";

        command.Parameters.Add(new SqlParameter("@CapabilityKey", SqlDbType.NVarChar, 200) { Value = capabilityKey });
        command.Parameters.Add(new SqlParameter("@Locale", SqlDbType.NVarChar, 20) { Value = NormalizeLocale(locale) });
        command.Parameters.Add(new SqlParameter("@DefaultLocale", SqlDbType.NVarChar, 20) { Value = "vi-VN" });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            arguments.Add(new CapabilityArgumentMetadata
            {
                ArgumentName = ReadString(reader, "ArgumentName") ?? string.Empty,
                ProcParameterName = ReadString(reader, "ProcParameterName") ?? string.Empty,
                DataType = ReadString(reader, "DataType") ?? string.Empty,
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
                        Aliases = ReadString(reader, "Aliases"),
                        Examples = ReadString(reader, "Examples")
                    }
                    : null
            });
        }

        return arguments;
    }

    private static string NormalizeLocale(string? locale) =>
        string.IsNullOrWhiteSpace(locale) ? "vi-VN" : locale;

    private static string? ReadString(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static bool ReadBoolean(SqlDataReader reader, string name) =>
        reader.GetBoolean(reader.GetOrdinal(name));

    private static int ReadInt32(SqlDataReader reader, string name) =>
        reader.GetInt32(reader.GetOrdinal(name));
}
