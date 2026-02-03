using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using TILSOFTAI.Api.Contracts.Chat;
using TILSOFTAI.Domain.Errors;
using TILSOFTAI.Domain.Validation;

namespace TILSOFTAI.Api.Filters;

/// <summary>
/// Action filter that validates input for chat endpoints before action execution.
/// </summary>
public sealed class InputValidationFilter : IAsyncActionFilter
{
    private readonly IInputValidator _inputValidator;
    private readonly ILogger<InputValidationFilter> _logger;

    public InputValidationFilter(IInputValidator inputValidator, ILogger<InputValidationFilter> logger)
    {
        _inputValidator = inputValidator ?? throw new ArgumentNullException(nameof(inputValidator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Find ChatApiRequest in action arguments
        var chatRequest = context.ActionArguments.Values
            .OfType<ChatApiRequest>()
            .FirstOrDefault();

        if (chatRequest is not null)
        {
            var validationResult = _inputValidator.ValidateUserInput(
                chatRequest.Input,
                InputContext.ForChatMessage());

            if (!validationResult.IsValid)
            {
                _logger.LogWarning(
                    "Input validation failed for chat request. Errors: {ErrorCount}, CorrelationId: {CorrelationId}",
                    validationResult.Errors.Count,
                    context.HttpContext.TraceIdentifier);

                var firstError = validationResult.Errors.FirstOrDefault();
                var errorResponse = new ErrorEnvelope
                {
                    Code = firstError?.Code ?? ErrorCode.InvalidInput,
                    Message = firstError?.Message ?? "Input validation failed.",
                    MessageKey = firstError?.MessageKey ?? "validation.invalid_input",
                    CorrelationId = context.HttpContext.TraceIdentifier,
                    Detail = new
                    {
                        field = firstError?.Field,
                        data = firstError?.Data
                    }
                };

                context.Result = new BadRequestObjectResult(errorResponse);
                return;
            }

            // Replace input with sanitized value
            if (validationResult.SanitizedValue is not null &&
                validationResult.SanitizedValue != chatRequest.Input)
            {
                chatRequest.Input = validationResult.SanitizedValue;
            }

            // Log warning if prompt injection was detected but not blocked
            if (validationResult.InjectionSeverity != PromptInjectionSeverity.None)
            {
                _logger.LogWarning(
                    "Prompt injection detected but not blocked. Severity: {Severity}, CorrelationId: {CorrelationId}",
                    validationResult.InjectionSeverity,
                    context.HttpContext.TraceIdentifier);
            }
        }

        await next();
    }
}

/// <summary>
/// Attribute to apply input validation to controller actions.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class ValidateInputAttribute : Attribute, IFilterFactory
{
    public bool IsReusable => true;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetRequiredService<InputValidationFilter>();
    }
}
