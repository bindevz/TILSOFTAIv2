namespace TILSOFTAI.Orchestration.Answering;

public interface IAnswerComposer
{
    Task<AssistantAnswer> ComposeAsync(
        AnswerComposerRequest request,
        CancellationToken cancellationToken);
}
