using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Errors;

namespace TILSOFTAI.Infrastructure.Errors;

public sealed class InMemoryErrorCatalog : IErrorCatalog
{
    private static readonly HashSet<string> SupportedLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        "en",
        "vi"
    };

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Messages =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            [ErrorCode.InvalidArgument] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "Invalid request parameters.",
                ["vi"] = "Tham so yeu cau khong hop le."
            },
            [ErrorCode.Unauthenticated] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "Authentication is required.",
                ["vi"] = "Can xac thuc truoc khi truy cap."
            },
            [ErrorCode.Unauthorized] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "Authentication is required.",
                ["vi"] = "Can xac thuc truoc khi truy cap."
            },
            [ErrorCode.RequestTooLarge] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "Request body too large.",
                ["vi"] = "Kich thuoc yeu cau qua lon."
            },
            [ErrorCode.Forbidden] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "You do not have permission to perform this action.",
                ["vi"] = "Ban khong co quyen thuc hien hanh dong nay."
            },
            [ErrorCode.NotFound] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "Requested resource was not found.",
                ["vi"] = "Khong tim thay tai nguyen yeu cau."
            },
            [ErrorCode.ToolValidationFailed] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "Tool validation failed.",
                ["vi"] = "Xac thuc cong cu that bai."
            },
            [ErrorCode.ToolExecutionFailed] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "Tool execution failed.",
                ["vi"] = "Thuc thi cong cu that bai."
            },
            [ErrorCode.ToolArgsInvalid] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "Tool arguments validation failed.",
                ["vi"] = "Xac thuc tham so cong cu that bai."
            },
            [ErrorCode.TenantMismatch] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "Tenant authorization failed.",
                ["vi"] = "Xac thuc tenant that bai."
            },
            [ErrorCode.WriteActionArgsInvalid] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "Write action arguments are invalid.",
                ["vi"] = "Tham so hanh dong ghi khong hop le."
            },
            [ErrorCode.WriteActionDisabled] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "Write action is currently disabled.",
                ["vi"] = "Hanh dong ghi hien tai bi vo hieu hoa."
            },
            [ErrorCode.WriteActionNotFound] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "Write action not found or not allowed.",
                ["vi"] = "Khong tim thay hanh dong ghi."
            },
            [ErrorCode.LlmTransportError] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "Failed to reach language model provider.",
                ["vi"] = "Khong ket noi duoc voi nha cung cap mo hinh."
            },
            [ErrorCode.SqlError] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "A database error occurred.",
                ["vi"] = "Da xay ra loi co so du lieu."
            },
            [ErrorCode.ChatFailed] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "Chat request failed.",
                ["vi"] = "Yeu cau chat that bai."
            },
            [ErrorCode.UnhandledError] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "An unexpected error occurred.",
                ["vi"] = "Da xay ra loi khong xac dinh."
            }
        };

    private readonly LocalizationOptions _localizationOptions;

    public InMemoryErrorCatalog(IOptions<LocalizationOptions> localizationOptions)
    {
        _localizationOptions = localizationOptions?.Value ?? throw new ArgumentNullException(nameof(localizationOptions));
    }

    public ErrorDefinition Get(string code, string language)
    {
        var normalizedCode = string.IsNullOrWhiteSpace(code) ? ErrorCode.UnhandledError : code.Trim();
        var resolvedLanguage = ResolveLanguage(language);

        if (Messages.TryGetValue(normalizedCode, out var localized)
            && localized.TryGetValue(resolvedLanguage, out var message))
        {
            return new ErrorDefinition(normalizedCode, resolvedLanguage, message);
        }

        if (Messages.TryGetValue(normalizedCode, out localized)
            && localized.TryGetValue("en", out var english))
        {
            return new ErrorDefinition(normalizedCode, resolvedLanguage, english);
        }

        if (Messages.TryGetValue(ErrorCode.UnhandledError, out var fallback)
            && fallback.TryGetValue(resolvedLanguage, out var fallbackMessage))
        {
            return new ErrorDefinition(normalizedCode, resolvedLanguage, fallbackMessage);
        }

        var defaultMessage = fallback?.TryGetValue("en", out var fallbackEnglish) == true
            ? fallbackEnglish
            : "An unexpected error occurred.";

        return new ErrorDefinition(normalizedCode, resolvedLanguage, defaultMessage);
    }

    private string ResolveLanguage(string? language)
    {
        var normalized = NormalizeLanguage(language);
        if (IsSupported(normalized))
        {
            return normalized!;
        }

        var fallback = NormalizeLanguage(_localizationOptions.DefaultLanguage);
        if (IsSupported(fallback))
        {
            return fallback!;
        }

        return "en";
    }

    private static bool IsSupported(string? language)
        => !string.IsNullOrWhiteSpace(language) && SupportedLanguages.Contains(language.Trim());

    private static string? NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return null;
        }

        var trimmed = language.Trim();
        var commaIndex = trimmed.IndexOf(',');
        if (commaIndex >= 0)
        {
            trimmed = trimmed[..commaIndex];
        }

        var dashIndex = trimmed.IndexOf('-');
        if (dashIndex >= 0)
        {
            trimmed = trimmed[..dashIndex];
        }

        var underscoreIndex = trimmed.IndexOf('_');
        if (underscoreIndex >= 0)
        {
            trimmed = trimmed[..underscoreIndex];
        }

        return trimmed;
    }
}
