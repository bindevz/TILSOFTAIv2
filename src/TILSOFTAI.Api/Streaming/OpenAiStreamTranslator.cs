using TILSOFTAI.Api.Contracts.Chat;
using TILSOFTAI.Api.Contracts.OpenAi;

namespace TILSOFTAI.Api.Streaming;

public sealed class OpenAiStreamTranslator
{
    private readonly string _id;
    private readonly long _created;
    private readonly string _model;

    public OpenAiStreamTranslator(string id, long created, string model)
    {
        _id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("id is required.", nameof(id)) : id;
        _created = created;
        _model = string.IsNullOrWhiteSpace(model) ? "unknown" : model;
    }

    public bool TryTranslate(
        ChatStreamEventEnvelope envelope,
        out OpenAiChatCompletionsStreamChunk? chunk,
        out bool isTerminal,
        out bool isError)
    {
        if (envelope is null)
        {
            throw new ArgumentNullException(nameof(envelope));
        }

        chunk = null;
        isTerminal = false;
        isError = false;

        switch (envelope.Type)
        {
            case "delta":
                if (envelope.Payload is string delta && !string.IsNullOrEmpty(delta))
                {
                    chunk = BuildChunk(delta, null);
                    return true;
                }
                return false;
            case "final":
                chunk = BuildChunk(null, "stop");
                isTerminal = true;
                return true;
            case "error":
                isTerminal = true;
                isError = true;
                return false;
            default:
                return false;
        }
    }

    private OpenAiChatCompletionsStreamChunk BuildChunk(string? deltaContent, string? finishReason)
    {
        return new OpenAiChatCompletionsStreamChunk
        {
            Id = _id,
            Created = _created,
            Model = _model,
            Choices =
            [
                new OpenAiChatStreamChoice
                {
                    Index = 0,
                    Delta = new OpenAiChatDelta { Content = deltaContent },
                    FinishReason = finishReason
                }
            ]
        };
    }
}
