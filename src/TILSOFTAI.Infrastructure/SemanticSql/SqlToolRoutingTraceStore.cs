using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Orchestration.Semantic;

namespace TILSOFTAI.Infrastructure.SemanticSql;

public sealed class SqlToolRoutingTraceStore : IToolRoutingTraceStore
{
    private readonly SqlOptions _sqlOptions;

    public SqlToolRoutingTraceStore(IOptions<SqlOptions> sqlOptions)
    {
        _sqlOptions = sqlOptions?.Value ?? throw new ArgumentNullException(nameof(sqlOptions));
    }

    public async Task SaveAsync(ToolRoutingTrace trace, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(trace);

        await using var connection = new SqlConnection(_sqlOptions.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandTimeout = _sqlOptions.CommandTimeoutSeconds;
        command.CommandText = @"
INSERT INTO ai.ToolRoutingTrace
(
    CorrelationID,
    TenantID,
    UserID,
    Locale,
    UserMessageHash,
    UserMessageRedacted,
    CandidateDomainsJson,
    CandidateToolsJson,
    HardSignalsJson,
    AdvertisedFunctionToolsJson,
    SelectedTool,
    SelectedFunction,
    CandidateDomainCount,
    CandidateCapabilityCount,
    AdvertisedToolCount,
    ArgumentsJson,
    ArgumentsBeforeNormalizationJson,
    ArgumentsAfterNormalizationJson,
    ValidationResultJson,
    AdapterType,
    [RowCount],
    AnswerMode,
    LatencyMs,
    LatencyByStageJson,
    ModelProvider,
    Success,
    ErrorCode
)
VALUES
(
    @CorrelationID,
    @TenantID,
    @UserID,
    @Locale,
    @UserMessageHash,
    @UserMessageRedacted,
    @CandidateDomainsJson,
    @CandidateToolsJson,
    @HardSignalsJson,
    @AdvertisedFunctionToolsJson,
    @SelectedTool,
    @SelectedFunction,
    @CandidateDomainCount,
    @CandidateCapabilityCount,
    @AdvertisedToolCount,
    @ArgumentsJson,
    @ArgumentsBeforeNormalizationJson,
    @ArgumentsAfterNormalizationJson,
    @ValidationResultJson,
    @AdapterType,
    @RowCount,
    @AnswerMode,
    @LatencyMs,
    @LatencyByStageJson,
    @ModelProvider,
    @Success,
    @ErrorCode
);";

        command.Parameters.Add(new SqlParameter("@CorrelationID", SqlDbType.UniqueIdentifier) { Value = trace.CorrelationId == Guid.Empty ? Guid.NewGuid() : trace.CorrelationId });
        command.Parameters.Add(new SqlParameter("@TenantID", SqlDbType.NVarChar, 100) { Value = trace.TenantId });
        command.Parameters.Add(new SqlParameter("@UserID", SqlDbType.NVarChar, 100) { Value = trace.UserId });
        command.Parameters.Add(new SqlParameter("@Locale", SqlDbType.NVarChar, 20) { Value = DbValue(trace.Locale) });
        command.Parameters.Add(new SqlParameter("@UserMessageHash", SqlDbType.VarBinary, 32) { Value = trace.UserMessageHash is null ? DBNull.Value : trace.UserMessageHash });
        command.Parameters.Add(new SqlParameter("@UserMessageRedacted", SqlDbType.NVarChar, -1) { Value = DbValue(trace.UserMessageRedacted) });
        command.Parameters.Add(new SqlParameter("@CandidateDomainsJson", SqlDbType.NVarChar, -1) { Value = DbValue(trace.CandidateDomainsJson) });
        command.Parameters.Add(new SqlParameter("@CandidateToolsJson", SqlDbType.NVarChar, -1) { Value = DbValue(trace.CandidateToolsJson) });
        command.Parameters.Add(new SqlParameter("@HardSignalsJson", SqlDbType.NVarChar, -1) { Value = DbValue(trace.HardSignalsJson) });
        command.Parameters.Add(new SqlParameter("@AdvertisedFunctionToolsJson", SqlDbType.NVarChar, -1) { Value = DbValue(trace.AdvertisedFunctionToolsJson) });
        command.Parameters.Add(new SqlParameter("@SelectedTool", SqlDbType.NVarChar, 200) { Value = DbValue(trace.SelectedTool) });
        command.Parameters.Add(new SqlParameter("@SelectedFunction", SqlDbType.NVarChar, 200) { Value = DbValue(trace.SelectedFunction) });
        command.Parameters.Add(new SqlParameter("@CandidateDomainCount", SqlDbType.Int) { Value = trace.CandidateDomainCount ?? (object)DBNull.Value });
        command.Parameters.Add(new SqlParameter("@CandidateCapabilityCount", SqlDbType.Int) { Value = trace.CandidateCapabilityCount ?? (object)DBNull.Value });
        command.Parameters.Add(new SqlParameter("@AdvertisedToolCount", SqlDbType.Int) { Value = trace.AdvertisedToolCount ?? (object)DBNull.Value });
        command.Parameters.Add(new SqlParameter("@ArgumentsJson", SqlDbType.NVarChar, -1) { Value = DbValue(trace.ArgumentsJson) });
        command.Parameters.Add(new SqlParameter("@ArgumentsBeforeNormalizationJson", SqlDbType.NVarChar, -1) { Value = DbValue(trace.ArgumentsBeforeNormalizationJson) });
        command.Parameters.Add(new SqlParameter("@ArgumentsAfterNormalizationJson", SqlDbType.NVarChar, -1) { Value = DbValue(trace.ArgumentsAfterNormalizationJson) });
        command.Parameters.Add(new SqlParameter("@ValidationResultJson", SqlDbType.NVarChar, -1) { Value = DbValue(trace.ValidationResultJson) });
        command.Parameters.Add(new SqlParameter("@AdapterType", SqlDbType.NVarChar, 100) { Value = DbValue(trace.AdapterType) });
        command.Parameters.Add(new SqlParameter("@RowCount", SqlDbType.Int) { Value = trace.RowCount ?? (object)DBNull.Value });
        command.Parameters.Add(new SqlParameter("@AnswerMode", SqlDbType.NVarChar, 50) { Value = DbValue(trace.AnswerMode) });
        command.Parameters.Add(new SqlParameter("@LatencyMs", SqlDbType.Int) { Value = trace.LatencyMs ?? (object)DBNull.Value });
        command.Parameters.Add(new SqlParameter("@LatencyByStageJson", SqlDbType.NVarChar, -1) { Value = DbValue(trace.LatencyByStageJson) });
        command.Parameters.Add(new SqlParameter("@ModelProvider", SqlDbType.NVarChar, 100) { Value = DbValue(trace.ModelProvider) });
        command.Parameters.Add(new SqlParameter("@Success", SqlDbType.Bit) { Value = trace.Success });
        command.Parameters.Add(new SqlParameter("@ErrorCode", SqlDbType.NVarChar, 100) { Value = DbValue(trace.ErrorCode) });

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static object DbValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
}
