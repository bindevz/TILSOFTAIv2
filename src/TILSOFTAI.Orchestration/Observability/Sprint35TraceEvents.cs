namespace TILSOFTAI.Orchestration.Observability;

public static class Sprint35TraceEvents
{
    public const string AgentRouteStarted = "agent_route_started";
    public const string CandidateSelectionCompleted = "candidate_selection_completed";
    public const string AgentCreated = "agent_created";
    public const string ToolsAdvertised = "tools_advertised";
    public const string AgentRunStarted = "agent_run_started";
    public const string AgentToolInvoked = "agent_tool_invoked";
    public const string CapabilityFacadeStarted = "capability_facade_started";
    public const string CapabilityValidationFailed = "capability_validation_failed";
    public const string CapabilityExecutionCompleted = "capability_execution_completed";
    public const string AnswerComposerStarted = "answer_composer_started";
    public const string AnswerComposerCompleted = "answer_composer_completed";
    public const string AgentRouteFailedClosed = "agent_route_failed_closed";
    public const string PendingActionCreated = "pending_action_created";
    public const string PendingActionConfirmed = "pending_action_confirmed";
    public const string PendingActionExpired = "pending_action_expired";
}
