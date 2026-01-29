namespace TILSOFTAI.Orchestration.Tools;

public interface IJsonSchemaValidator
{
    JsonSchemaValidationResult Validate(string schemaJson, string instanceJson);
}

public sealed record JsonSchemaValidationResult(bool IsValid, string? Error);
