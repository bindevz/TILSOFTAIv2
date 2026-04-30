using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace TILSOFTAI.Orchestration.Answering.Narration;

public sealed class AgentAnswerNarrationService : IAnswerNarrationService
{
    private readonly IChatClient? _chatClient;
    private readonly AnswerNarrationPromptBuilder _promptBuilder;
    private readonly AnswerNarrationResponseParser _responseParser;
    private readonly GenericSchemaSummaryFallback _fallback;
    private readonly AnswerNarrationPolicy _policy;

    public AgentAnswerNarrationService(
        IEnumerable<IChatClient> chatClients,
        AnswerNarrationPromptBuilder promptBuilder,
        AnswerNarrationResponseParser responseParser,
        GenericSchemaSummaryFallback fallback,
        IOptions<AnswerNarrationPolicy> policy)
    {
        _chatClient = chatClients?.FirstOrDefault();
        _promptBuilder = promptBuilder ?? throw new ArgumentNullException(nameof(promptBuilder));
        _responseParser = responseParser ?? throw new ArgumentNullException(nameof(responseParser));
        _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
        _policy = policy?.Value ?? new AnswerNarrationPolicy();
    }

    public async Task<AnswerNarrationResult> GenerateAsync(
        AnswerNarrationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var effectiveRequest = request with
        {
            Rows = request.Rows.Take(Math.Max(1, _policy.MaxRowsForNarration)).ToArray()
        };

        if (!_policy.Enabled || !_policy.UseAi || _chatClient is null)
        {
            return _fallback.Generate(effectiveRequest);
        }

        try
        {
            var messages = new[]
            {
                new ChatMessage(ChatRole.System, _promptBuilder.BuildSystemInstruction()),
                new ChatMessage(ChatRole.User, _promptBuilder.BuildUserMessage(effectiveRequest))
            };

            var response = await _chatClient.GetResponseAsync(
                    messages,
                    options: null,
                    cancellationToken)
                .ConfigureAwait(false);

            if (_responseParser.TryParse(
                    response.Text,
                    effectiveRequest,
                    Math.Max(1, _policy.MaxOutputCharacters),
                    out var parsed))
            {
                return parsed;
            }
        }
        catch (Exception) when (_policy.FallbackOnInvalidOutput)
        {
            return _fallback.Generate(effectiveRequest);
        }

        return _fallback.Generate(effectiveRequest);
    }
}
