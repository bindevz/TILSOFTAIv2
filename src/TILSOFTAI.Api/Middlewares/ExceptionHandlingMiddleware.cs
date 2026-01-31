using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Errors;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Domain.Security;
using TILSOFTAI.Orchestration.Observability;

namespace TILSOFTAI.Api.Middlewares;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IErrorCatalog _errorCatalog;
    private readonly ISqlErrorLogWriter _errorLogWriter;
    private readonly ObservabilityOptions _observabilityOptions;
    private readonly AuthOptions _authOptions;
    private readonly ErrorHandlingOptions _errorHandlingOptions;
    private readonly IdentityResolutionPolicy _identityPolicy;
    private readonly IWebHostEnvironment _hostEnvironment;
    private readonly ILogRedactor _logRedactor;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        IErrorCatalog errorCatalog,
        ISqlErrorLogWriter errorLogWriter,
        IOptions<AuthOptions> authOptions,
        IOptions<ObservabilityOptions> observabilityOptions,
        IOptions<ErrorHandlingOptions> errorHandlingOptions,
        IdentityResolutionPolicy identityPolicy,
        IWebHostEnvironment hostEnvironment,
        ILogRedactor logRedactor,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _errorCatalog = errorCatalog ?? throw new ArgumentNullException(nameof(errorCatalog));
        _errorLogWriter = errorLogWriter ?? throw new ArgumentNullException(nameof(errorLogWriter));
        _authOptions = authOptions?.Value ?? throw new ArgumentNullException(nameof(authOptions));
        _observabilityOptions = observabilityOptions?.Value ?? throw new ArgumentNullException(nameof(observabilityOptions));
        _errorHandlingOptions = errorHandlingOptions?.Value ?? throw new ArgumentNullException(nameof(errorHandlingOptions));
        _identityPolicy = identityPolicy ?? throw new ArgumentNullException(nameof(identityPolicy));
        _hostEnvironment = hostEnvironment ?? throw new ArgumentNullException(nameof(hostEnvironment));
        _logRedactor = logRedactor ?? throw new ArgumentNullException(nameof(logRedactor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)
            {
                _logger.LogError(ex, "Exception thrown after the response started.");
                throw;
            }

            var identity = _identityPolicy.ResolveForError(context, _authOptions, _hostEnvironment);
            var resolvedContext = BuildExecutionContext(identity, context);

            var (code, status, rawDetail) = MapException(ex);
            if (identity.IsHeaderSpoofAttempt)
            {
                code = ErrorCode.TenantMismatch;
                status = StatusCodes.Status403Forbidden;
                rawDetail = new { suspicious_identity_header = true };
                _logger.LogWarning("Suspicious identity header detected during error handling.");
            }

            var definition = _errorCatalog.Get(code, resolvedContext.Language);
            resolvedContext.Language = definition.Language;

            var detail = BuildClientDetail(code, rawDetail, identity);

            var error = new ErrorEnvelope
            {
                Code = code,
                MessageKey = code,
                LocalizedMessage = definition.MessageTemplate,
                Message = definition.MessageTemplate,
                Detail = detail,
                CorrelationId = resolvedContext.CorrelationId,
                TraceId = resolvedContext.TraceId,
                RequestId = resolvedContext.RequestId
            };

            var payload = new
            {
                success = false,
                error,
                correlationId = resolvedContext.CorrelationId,
                conversationId = resolvedContext.ConversationId,
                traceId = resolvedContext.TraceId,
                language = resolvedContext.Language
            };

            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(
                payload,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase },
                context.RequestAborted);

            if (_observabilityOptions.EnableSqlErrorLog)
            {
                try
                {
                    await _errorLogWriter.WriteAsync(resolvedContext, code, error.Message, rawDetail, context.RequestAborted);
                }
                catch (Exception logEx)
                {
                    _logger.LogError(logEx, "Failed to persist error log entry.");
                }
            }

            if (status >= StatusCodes.Status500InternalServerError)
            {
                _logger.LogError(ex, "Unhandled exception mapped to {ErrorCode}.", code);
            }
            else
            {
                _logger.LogWarning(ex, "Request failed with {ErrorCode}.", code);
            }
        }
    }

    private static TilsoftExecutionContext BuildExecutionContext(IdentityResolutionResult identity, HttpContext context)
    {
        var tenantId = ResolveTenantId(identity.TenantId, context);
        var userId = identity.UserId ?? string.Empty;

        return new TilsoftExecutionContext
        {
            TenantId = tenantId,
            UserId = userId,
            Roles = identity.Roles ?? Array.Empty<string>(),
            CorrelationId = identity.CorrelationId,
            ConversationId = identity.ConversationId,
            RequestId = identity.RequestId,
            TraceId = identity.TraceId,
            Language = identity.Language
        };
    }

    private static string ResolveTenantId(string? tenantId, HttpContext context)
    {
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            return tenantId;
        }

        var allowAnonymous = context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() != null;
        return allowAnonymous ? "public" : string.Empty;
    }

    private object? BuildClientDetail(string code, object? rawDetail, IdentityResolutionResult identity)
    {
        // Always return validation details for validation codes, regardless of detail policy
        // This ensures structured error paths are actionable in production
        if (IsValidationCode(code))
        {
            return BuildValidationDetails(code, rawDetail);
        }

        // Apply detail policy for other error codes
        if (!IsDetailAllowed(identity))
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

    private bool IsDetailAllowed(IdentityResolutionResult identity)
    {
        var allowByDev = _hostEnvironment.IsDevelopment() && _errorHandlingOptions.ExposeErrorDetailInDevelopment;
        var allowByRole = _errorHandlingOptions.ExposeErrorDetail && HasAnyRole(identity.Roles, _errorHandlingOptions.ExposeErrorDetailRoles);
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

    private static (string Code, int Status, object? Detail) MapException(Exception ex)
    {
        return ex switch
        {
            TilsoftApiException apiEx => (apiEx.Code, apiEx.HttpStatusCode, apiEx.Detail),
            ArgumentException or JsonException => (ErrorCode.InvalidArgument, StatusCodes.Status400BadRequest, null),
            UnauthorizedAccessException authEx when IsUnauthenticated(authEx)
                => (ErrorCode.Unauthenticated, StatusCodes.Status401Unauthorized, null),
            UnauthorizedAccessException => (ErrorCode.Unauthorized, StatusCodes.Status401Unauthorized, null),
            SqlException => (ErrorCode.SqlError, StatusCodes.Status500InternalServerError, null),
            InvalidOperationException => (ErrorCode.ChatFailed, StatusCodes.Status400BadRequest, null),
            _ => (ErrorCode.UnhandledError, StatusCodes.Status500InternalServerError, null)
        };
    }

    private static bool IsUnauthenticated(UnauthorizedAccessException exception)
    {
        return string.Equals(exception.Message, ErrorCode.Unauthenticated, StringComparison.OrdinalIgnoreCase);
    }
}
