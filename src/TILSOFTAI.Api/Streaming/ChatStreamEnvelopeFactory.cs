using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using TILSOFTAI.Api.Contracts.Chat;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Errors;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Observability;
using TILSOFTAI.Orchestration.Pipeline;

namespace TILSOFTAI.Api.Streaming;

public sealed class ChatStreamEnvelopeFactory
{
    private readonly IErrorCatalog _errorCatalog;
    private readonly ErrorHandlingOptions _errorHandlingOptions;
    private readonly IWebHostEnvironment _hostEnvironment;
    private readonly ILogRedactor _logRedactor;

    public ChatStreamEnvelopeFactory(
        IErrorCatalog errorCatalog,
        IOptions<ErrorHandlingOptions> errorHandlingOptions,
        IWebHostEnvironment hostEnvironment,
        ILogRedactor logRedactor)
    {
        _errorCatalog = errorCatalog ?? throw new ArgumentNullException(nameof(errorCatalog));
        _errorHandlingOptions = errorHandlingOptions?.Value ?? throw new ArgumentNullException(nameof(errorHandlingOptions));
        _hostEnvironment = hostEnvironment ?? throw new ArgumentNullException(nameof(hostEnvironment));
        _logRedactor = logRedactor ?? throw new ArgumentNullException(nameof(logRedactor));
    }

    public ChatStreamEventEnvelope Create(ChatStreamEvent streamEvent, TilsoftExecutionContext context)
    {
        if (streamEvent is null)
        {
            throw new ArgumentNullException(nameof(streamEvent));
        }

        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        return new ChatStreamEventEnvelope
        {
            Type = streamEvent.Type,
            Payload = streamEvent.Type == "error"
                ? CreateErrorPayload(streamEvent.Payload, context)
                : streamEvent.Payload,
            ConversationId = context.ConversationId,
            CorrelationId = context.CorrelationId,
            TraceId = context.TraceId,
            Language = context.Language
        };
    }

    private object CreateErrorPayload(object? detail, TilsoftExecutionContext context)
    {
        if (detail is ErrorEnvelope errorEnvelope)
        {
            var code = string.IsNullOrWhiteSpace(errorEnvelope.Code) ? ErrorCode.ChatFailed : errorEnvelope.Code;
            return BuildErrorEnvelope(code, errorEnvelope.Detail, context);
        }

        return BuildErrorEnvelope(ErrorCode.ChatFailed, detail, context);
    }

    private ErrorEnvelope BuildErrorEnvelope(string code, object? rawDetail, TilsoftExecutionContext context)
    {
        var definition = _errorCatalog.Get(code, context.Language);
        var detail = BuildClientDetail(code, rawDetail, context.Roles);

        return new ErrorEnvelope
        {
            Code = code,
            MessageKey = code,
            LocalizedMessage = definition.MessageTemplate,
            Message = definition.MessageTemplate,
            Detail = detail,
            CorrelationId = context.CorrelationId,
            TraceId = context.TraceId,
            RequestId = context.RequestId
        };
    }

    private object? BuildClientDetail(string code, object? rawDetail, string[]? roles)
    {
        // Always return validation details for validation codes, regardless of detail policy
        // This ensures structured error paths are actionable in production
        if (IsValidationCode(code))
        {
            return BuildValidationDetails(code, rawDetail);
        }

        // Apply detail policy for other error codes
        if (!IsDetailAllowed(roles))
        {
            return null;
        }

        if (rawDetail is null)
        {
            return null;
        }

        var detailText = rawDetail is string text
            ? text
            : JsonSerializer.Serialize(rawDetail);

        var redacted = _logRedactor.RedactForClient(detailText).redacted;
        if (redacted.Length > _errorHandlingOptions.MaxDetailLength)
        {
            redacted = redacted[.._errorHandlingOptions.MaxDetailLength];
        }

        return redacted;
    }

    private bool IsDetailAllowed(string[]? roles)
    {
        var allowByDev = _hostEnvironment.IsDevelopment() && _errorHandlingOptions.ExposeErrorDetailInDevelopment;
        var allowByRole = _errorHandlingOptions.ExposeErrorDetail && HasAnyRole(roles, _errorHandlingOptions.ExposeErrorDetailRoles);
        return allowByDev || allowByRole;
    }

    private static bool HasAnyRole(string[]? roles, string[]? allowedRoles)
    {
        if (roles is null || allowedRoles is null || roles.Length == 0 || allowedRoles.Length == 0)
        {
            return false;
        }

        var allowed = new HashSet<string>(allowedRoles.Where(r => !string.IsNullOrWhiteSpace(r)), StringComparer.OrdinalIgnoreCase);
        return roles.Any(role => allowed.Contains(role));
    }

    private static bool IsValidationCode(string code)
    {
        return string.Equals(code, ErrorCode.InvalidArgument, StringComparison.OrdinalIgnoreCase)
               || string.Equals(code, ErrorCode.ToolArgsInvalid, StringComparison.OrdinalIgnoreCase);
    }

    private static object BuildValidationDetails(string code, object? rawDetail)
    {
        var details = new List<object>();

        if (rawDetail is IEnumerable<string> errors)
        {
            foreach (var error in errors)
            {
                var path = ParseValidationPath(error);
                details.Add(new { path, messageKey = code });
            }
        }
        else if (rawDetail is string errorText)
        {
            var path = ParseValidationPath(errorText);
            details.Add(new { path, messageKey = code });
        }

        if (details.Count == 0)
        {
            details.Add(new { path = "/", messageKey = code });
        }

        return details;
    }

    private static string ParseValidationPath(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return "/";
        }

        var separator = error.IndexOf(':');
        if (separator > 0)
        {
            var candidate = error[..separator].Trim();
            return string.IsNullOrWhiteSpace(candidate) ? "/" : candidate;
        }

        return "/";
    }
}
