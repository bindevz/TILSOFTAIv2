namespace TILSOFTAI.Supervisor.Classification;

/// <summary>
/// Classifies user input to determine which domain agent should handle it.
/// Sprint 2: keyword-based. Future sprints may use LLM-based classification.
/// </summary>
public interface IIntentClassifier
{
    Task<IntentClassification> ClassifyAsync(string input, CancellationToken ct);
}
