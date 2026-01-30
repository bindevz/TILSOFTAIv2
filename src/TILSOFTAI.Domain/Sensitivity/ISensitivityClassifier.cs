namespace TILSOFTAI.Domain.Sensitivity;

/// <summary>
/// Classifies text content for sensitivity (PII, secrets, etc.).
/// Used to enforce cache governance and log redaction policies server-side.
/// </summary>
public interface ISensitivityClassifier
{
    /// <summary>
    /// Analyzes the given text and returns a classification result.
    /// </summary>
    /// <param name="text">The text to analyze.</param>
    /// <returns>A SensitivityResult indicating whether sensitive content was detected and why.</returns>
    SensitivityResult Classify(string text);
}
