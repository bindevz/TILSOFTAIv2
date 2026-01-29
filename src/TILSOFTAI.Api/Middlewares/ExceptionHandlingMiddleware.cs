using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using TILSOFTAI.Api.Auth;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Errors;
using TILSOFTAI.Domain.ExecutionContext;

namespace TILSOFTAI.Api.Middlewares;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IErrorCatalog _errorCatalog;
    private readonly IExecutionContextAccessor _contextAccessor;
    private readonly ISqlErrorLogWriter _errorLogWriter;
    private readonly ObservabilityOptions _observabilityOptions;
    private readonly AuthOptions _authOptions;
    private readonly LocalizationOptions _localizationOptions;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        IErrorCatalog errorCatalog,
        IExecutionContextAccessor contextAccessor,
        ISqlErrorLogWriter errorLogWriter,
        IOptions<AuthOptions> authOptions,
        IOptions<ObservabilityOptions> observabilityOptions,
        IOptions<LocalizationOptions> localizationOptions,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _errorCatalog = errorCatalog ?? throw new ArgumentNullException(nameof(errorCatalog));
        _contextAccessor = contextAccessor ?? throw new ArgumentNullException(nameof(contextAccessor));
        _errorLogWriter = errorLogWriter ?? throw new ArgumentNullException(nameof(errorLogWriter));
        _authOptions = authOptions?.Value ?? throw new ArgumentNullException(nameof(authOptions));
        _observabilityOptions = observabilityOptions?.Value ?? throw new ArgumentNullException(nameof(observabilityOptions));
        _localizationOptions = localizationOptions?.Value ?? throw new ArgumentNullException(nameof(localizationOptions));
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

            TilsoftExecutionContext resolvedContext;
            var effectiveException = ex;
            try
            {
                resolvedContext = ResolveContext(context, throwOnFailure: true);
            }
            catch (UnauthorizedAccessException authEx)
            {
                effectiveException = authEx;
                resolvedContext = ResolveContext(context, throwOnFailure: false);
            }

            var (code, status, detail) = MapException(effectiveException);
            var definition = _errorCatalog.Get(code, resolvedContext.Language);

            resolvedContext.Language = definition.Language;

            var error = new ErrorEnvelope
            {
                Code = code,
                Message = definition.MessageTemplate,
                Detail = detail
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
                    await _errorLogWriter.WriteAsync(resolvedContext, code, error.Message, detail, context.RequestAborted);
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

    private TilsoftExecutionContext ResolveContext(HttpContext context, bool throwOnFailure)
    {
        var existing = _contextAccessor.Current;
        var traceId = !string.IsNullOrWhiteSpace(existing.TraceId)
            ? existing.TraceId
            : Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

        var correlationId = FirstNonEmpty(existing.CorrelationId, GetHeader(context, "X-Correlation-Id")) ?? traceId;
        var conversationId = FirstNonEmpty(existing.ConversationId, GetHeader(context, "X-Conversation-Id")) ?? traceId;
        var requestId = FirstNonEmpty(existing.RequestId) ?? traceId;
        
        // Use existing context values if present
        var tenantId = existing.TenantId;
        var userId = existing.UserId;
        
        // Only resolve from claims/headers if context is empty
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(userId))
        {
            var tenantClaim = ExecutionContextResolver.GetClaim(context.User, _authOptions.TenantClaimName);
            var userClaim = ExecutionContextResolver.GetClaim(context.User, _authOptions.UserIdClaimName);
            var headerTenant = GetFirstHeader(context, _authOptions.HeaderTenantKeys);
            var headerUser = GetFirstHeader(context, _authOptions.HeaderUserKeys);
            var resolvedTenant = ResolveIdentity(tenantClaim, headerTenant, throwOnFailure, _authOptions.TenantClaimName);
            var resolvedUser = ResolveIdentity(userClaim, headerUser, throwOnFailure, _authOptions.UserIdClaimName);

            tenantId = FirstNonEmpty(tenantId, resolvedTenant);
            userId = FirstNonEmpty(userId, resolvedUser);
        }

        // For anonymous endpoints, allow public/unknown defaults
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(userId))
        {
            if (throwOnFailure)
            {
                throw new UnauthorizedAccessException("tenant_id and user_id are required.");
            }

            tenantId ??= "public";
            userId ??= "anonymous";
        }

        var language = !string.IsNullOrWhiteSpace(existing.Language)
            ? existing.Language
            : ExecutionContextResolver.ResolveLanguage(
                ExecutionContextResolver.GetClaim(context.User, TilsoftClaims.Language),
                GetHeader(context, "X-Lang"),
                GetHeader(context, "Accept-Language"),
                _localizationOptions);

        return new TilsoftExecutionContext
        {
            TenantId = tenantId,
            UserId = userId,
            Roles = existing.Roles ?? Array.Empty<string>(),
            CorrelationId = correlationId,
            ConversationId = conversationId,
            RequestId = requestId,
            TraceId = traceId,
            Language = language
        };
    }

    private string? ResolveIdentity(string? claimValue, string? headerValue, bool throwOnFailure, string fieldName)
    {
        try
        {
            return ExecutionContextResolver.ResolveIdentity(
                claimValue,
                headerValue,
                _authOptions.AllowHeaderFallback,
                fieldName);
        }
        catch (UnauthorizedAccessException)
        {
            if (throwOnFailure)
            {
                throw;
            }

            return ExecutionContextResolver.ResolveIdentity(
                claimValue,
                null,
                _authOptions.AllowHeaderFallback,
                fieldName);
        }
    }

    private string? GetFirstHeader(HttpContext context, string[] headerKeys)
    {
        foreach (var key in headerKeys)
        {
            var value = GetHeader(context, key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }
        return null;
    }

    private static string? GetHeader(HttpContext context, string name)
        => context.Request.Headers.TryGetValue(name, out var values) ? values.ToString() : null;

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static (string Code, int Status, object? Detail) MapException(Exception ex)
    {
        return ex switch
        {
            ArgumentException or JsonException => (ErrorCode.InvalidArgument, StatusCodes.Status400BadRequest, ex.Message),
            UnauthorizedAccessException => (ErrorCode.Unauthorized, StatusCodes.Status401Unauthorized, null),
            SqlException => (ErrorCode.SqlError, StatusCodes.Status500InternalServerError, null),
            InvalidOperationException => (ErrorCode.ChatFailed, StatusCodes.Status400BadRequest, null),
            _ => (ErrorCode.UnhandledError, StatusCodes.Status500InternalServerError, null)
        };
    }
}
