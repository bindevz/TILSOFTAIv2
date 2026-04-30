namespace TILSOFTAI.Orchestration.Answering.Narration;

public interface IAnswerNarrationService
{
    Task<AnswerNarrationResult> GenerateAsync(
        AnswerNarrationRequest request,
        CancellationToken cancellationToken);
}
