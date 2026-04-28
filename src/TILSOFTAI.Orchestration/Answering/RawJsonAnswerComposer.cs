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
        var result = request.Result is CompositeResultBundle bundle
            ? SanitizeBundle(bundle, request.SensitivityPolicy)
            : request.Result;
        var data = new
        {
            capabilityKey = request.CapabilityKey,
            procedureName = request.ProcedureName,
            arguments = request.Arguments,
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

    private static CompositeResultBundle SanitizeBundle(
        CompositeResultBundle bundle,
        SensitivityPolicy policy) =>
        bundle with
        {
            Sections = bundle.Sections
                .Select(section => section with
                {
                    Rows = AnswerDataSanitizer.ApplySensitivity(section.Rows, policy)
                })
                .ToArray()
        };

    private static AnswerProvenance CreateProvenance(AnswerComposerRequest request) => new()
    {
        CapabilityKey = request.CapabilityKey,
        RowCount = request.RowCount,
        CorrelationId = request.ExecutionMetadata.CorrelationId,
        ProcedureName = request.ProcedureName
    };
}
