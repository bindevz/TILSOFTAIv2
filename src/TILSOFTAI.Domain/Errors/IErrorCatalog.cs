namespace TILSOFTAI.Domain.Errors;

public interface IErrorCatalog
{
    ErrorDefinition Get(string code, string language);
    
    /// <summary>
    /// Attempts to get an exact error definition for the specified code and language.
    /// Returns true only if an exact translation exists (no fallback to English or UnhandledError).
    /// </summary>
    bool TryGetExact(string code, string language, out ErrorDefinition? definition);
}
