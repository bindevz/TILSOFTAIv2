using Microsoft.Extensions.Logging;

namespace TILSOFTAI.Agents.Abstractions;

/// <summary>
/// Sprint 3: Static utility that enforces the write-path governance rule:
/// domain agents must NOT execute writes directly — all write intents must
/// route through IApprovalEngine before execution.
/// </summary>
public static class AgentWritePolicy
{
    /// <summary>
    /// Verifies that a task marked for write preparation is handled correctly by the agent layer.
    /// Logs a governance trace when write intent is detected.
    /// </summary>
    public static void EnforceWriteGovernance(AgentTask task, string agentId, ILogger logger)
    {
        if (task is null || !task.RequiresWritePreparation)
        {
            return;
        }

        // Sprint 3: write preparation detected — log governance trace.
        // The actual enforcement happens at the SqlToolAdapter layer (IWriteActionGuard);
        // this log ensures visibility that a write intent reached the agent layer.
        logger.LogInformation(
            "AgentWritePolicy | AgentId: {AgentId} | WritePreparation: detected | " +
            "Enforcement: write execution will require IApprovalEngine approval before SqlToolAdapter proceeds",
            agentId);
    }
}
