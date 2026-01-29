namespace TILSOFTAI.Domain.Errors;

public interface IErrorCatalog
{
    ErrorDefinition Get(string code, string language);
}
