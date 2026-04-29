namespace TILSOFTAI.Orchestration.Observability;

public static class AgentRoutingTraceEvents
{
    public const string RouteStarted = "agent_route_started";
    public const string CandidateSelectionCompleted = "candidate_selection_completed";
    public const string ToolsAdvertised = "tools_advertised";
    public const string AgentRunStarted = "agent_run_started";
    public const string AgentToolInvoked = "agent_tool_invoked";
    public const string AnswerComposerStarted = "answer_composer_started";
    public const string AnswerComposerCompleted = "answer_composer_completed";
    public const string RouteFailedClosed = "agent_route_failed_closed";
    public const string PendingActionCreated = "pending_action_created";
    public const string PendingActionConfirmed = "pending_action_confirmed";
    public const string PendingActionExpired = "pending_action_expired";
}
