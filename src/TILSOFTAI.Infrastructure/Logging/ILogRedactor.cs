namespace TILSOFTAI.Infrastructure.Logging
{
    public interface ILogRedactor
    {
        object? Redact(string key, object? value);
    }
}
