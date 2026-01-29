namespace TILSOFTAI.Orchestration.Tools;

internal static class ToolValidationLocalizer
{
    public static string ToolNotEnabled(string toolName, string? language)
    {
        return Resolve(language) switch
        {
            "vi" => $"Cong cu '{toolName}' khong duoc bat.",
            _ => $"Tool '{toolName}' is not enabled."
        };
    }

    public static string ToolRequiresRoles(string toolName, IReadOnlyList<string> roles, string? language)
    {
        var roleText = string.Join(", ", roles);
        return Resolve(language) switch
        {
            "vi" => $"Cong cu '{toolName}' yeu cau quyen: {roleText}.",
            _ => $"Tool '{toolName}' requires roles: {roleText}."
        };
    }

    public static string ToolInvalidSpName(string toolName, string? language)
    {
        return Resolve(language) switch
        {
            "vi" => $"Cong cu '{toolName}' co tien to SpName khong hop le.",
            _ => $"Tool '{toolName}' has invalid SpName prefix."
        };
    }

    public static string ToolSchemaInvalid(string errorDetail, string? language)
    {
        return Resolve(language) switch
        {
            "vi" => $"Tham so cong cu khong dung theo schema: {errorDetail}",
            _ => $"Tool arguments failed schema validation: {errorDetail}"
        };
    }

    private static string Resolve(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return "en";
        }

        var trimmed = language.Trim();
        if (trimmed.StartsWith("vi", StringComparison.OrdinalIgnoreCase))
        {
            return "vi";
        }

        return "en";
    }
}
