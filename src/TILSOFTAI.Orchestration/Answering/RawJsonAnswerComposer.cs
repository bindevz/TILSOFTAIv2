namespace TILSOFTAI.Orchestration.Answering;

using TILSOFTAI.Orchestration.Execution;

public sealed class RawJsonAnswerComposer
{
    public Task<AssistantAnswer> ComposeAsync(
        AnswerComposerRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rows = AnswerDataSanitizer.ApplySensitivity(request.Rows, request.SensitivityPolicy);
        var arguments = AnswerDataSanitizer.ApplySensitivity(request.Arguments, request.SensitivityPolicy);
        var result = request.Result is CompositeResultBundle bundle
            ? AnswerDataSanitizer.ApplySensitivity(bundle, request.SensitivityPolicy)
            : AnswerDataSanitizer.ApplySensitivity(request.Result, request.SensitivityPolicy);
        var data = new
        {
            mode = "raw_json",
            capabilityKey = request.CapabilityKey,
            procedureName = request.ProcedureName,
            arguments,
            rowCount = request.RowCount,
            rows,
            result,
            resultSchema = request.ResultSchema,
            executionMetadata = request.ExecutionMetadata,
            provenance = CreateProvenance(request)
        };

        var answer = new AssistantAnswer
        {
            AnswerType = "raw_json",
            Text = string.Empty,
            Blocks = [new RawJsonBlock(data)],
            Provenance = CreateProvenance(request),
            SelectedAgentId = "microsoft-agent-router",
            Detail = data
        };

        return Task.FromResult(answer);
    }

    private static AnswerProvenance CreateProvenance(AnswerComposerRequest request) => new()
    {
        CapabilityKey = request.CapabilityKey,
        RowCount = request.RowCount,
        CorrelationId = request.ExecutionMetadata.CorrelationId,
        ProcedureName = request.ProcedureName
    };
}
