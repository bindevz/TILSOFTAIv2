using Microsoft.Extensions.Logging;

namespace TILSOFTAI.Orchestration.AiRouting;

public sealed class NoOpAgentToolRouter : IAgentToolRouter
{
    private readonly ILogger<NoOpAgentToolRouter> _logger;

    public NoOpAgentToolRouter(ILogger<NoOpAgentToolRouter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<AgentToolRoutingResult> TryRouteAsync(
        AgentToolRoutingRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "AgentToolRouterStubNotHandled | Locale: {Locale} | AnswerMode: {AnswerMode}",
            request.Locale,
            request.RequestedAnswerMode);

        return Task.FromResult(new AgentToolRoutingResult
        {
            Handled = false,
            FailureReason = "Agent Framework routing is not implemented yet."
        });
    }
}
