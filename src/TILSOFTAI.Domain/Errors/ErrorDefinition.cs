namespace TILSOFTAI.Domain.Errors;

public sealed record ErrorDefinition(string Code, string Language, string MessageTemplate);
