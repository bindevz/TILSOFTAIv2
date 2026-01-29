namespace TILSOFTAI.Domain.Configuration;

public sealed class LocalizationOptions
{
    public string DefaultLanguage { get; set; } = "en";
    public string[] SupportedLanguages { get; set; } = Array.Empty<string>();
}
