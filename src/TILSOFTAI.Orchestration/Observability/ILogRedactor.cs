namespace TILSOFTAI.Orchestration.Observability;

public interface ILogRedactor
{
    /// <summary>
    /// Redacts PII from plain text.
    /// </summary>
    /// <param name="text">Text to redact</param>
    /// <returns>Tuple of (redacted text, whether any redaction occurred)</returns>
    (string redacted, bool changed) RedactText(string text);

    /// <summary>
    /// Redacts PII from JSON string.
    /// </summary>
    /// <param name="json">JSON string to redact</param>
    /// <returns>Tuple of (redacted JSON, whether any redaction occurred)</returns>
    (string redacted, bool changed) RedactJson(string json);

    /// <summary>
    /// Redacts client-facing error detail (stricter than log redaction).
    /// </summary>
    /// <param name="text">Text to redact for client responses.</param>
    /// <returns>Tuple of (redacted text, whether any redaction occurred)</returns>
    (string redacted, bool changed) RedactForClient(string text);
}
