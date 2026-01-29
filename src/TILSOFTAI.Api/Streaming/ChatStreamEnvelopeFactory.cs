using TILSOFTAI.Api.Contracts.Chat;
using TILSOFTAI.Domain.Errors;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Pipeline;

namespace TILSOFTAI.Api.Streaming;

public sealed class ChatStreamEnvelopeFactory
{
    private readonly IErrorCatalog _errorCatalog;

    public ChatStreamEnvelopeFactory(IErrorCatalog errorCatalog)
    {
        _errorCatalog = errorCatalog ?? throw new ArgumentNullException(nameof(errorCatalog));
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
                ? CreateErrorPayload(streamEvent.Payload, context.Language)
                : streamEvent.Payload,
            ConversationId = context.ConversationId,
            CorrelationId = context.CorrelationId,
            TraceId = context.TraceId,
            Language = context.Language
        };
    }

    private object CreateErrorPayload(object? detail, string language)
    {
        if (detail is ErrorEnvelope errorEnvelope)
        {
            var definition = _errorCatalog.Get(errorEnvelope.Code, language);
            var message = string.IsNullOrWhiteSpace(errorEnvelope.Message)
                ? definition.MessageTemplate
                : errorEnvelope.Message;

            return new
            {
                code = string.IsNullOrWhiteSpace(errorEnvelope.Code) ? definition.Code : errorEnvelope.Code,
                message,
                detail = errorEnvelope.Detail,
                language = definition.Language
            };
        }

        var fallback = _errorCatalog.Get(ErrorCode.ChatFailed, language);
        return new
        {
            code = fallback.Code,
            message = fallback.MessageTemplate,
            detail,
            language = fallback.Language
        };
    }
}
