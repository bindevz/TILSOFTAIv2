namespace TILSOFTAI.Orchestration.AiRouting;

public interface IAgentToolRouter
{
    Task<AgentToolRoutingResult> TryRouteAsync(
        AgentToolRoutingRequest request,
        CancellationToken cancellationToken);
}

public interface IOfficialAgentToolRouter : IAgentToolRouter;
