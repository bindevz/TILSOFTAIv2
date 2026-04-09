using Microsoft.Extensions.Logging;

namespace TILSOFTAI.Agents.Domain;

/// <summary>
/// Sprint 2 warehouse domain agent skeleton.
/// Routes to warehouse domain; delegates execution via LegacyChatPipelineBridge.
/// Does not own specific tool prefixes yet (Option 4).
/// </summary>
public sealed class WarehouseAgent : DomainAgentBase
{
    public WarehouseAgent(LegacyChatPipelineBridge bridge, ILogger<WarehouseAgent> logger)
        : base(bridge, logger)
    {
    }

    public override string AgentId => "warehouse";
    public override string DisplayName => "Warehouse Domain Agent";
    public override IReadOnlyList<string> OwnedDomains { get; } = new[] { "warehouse" };
}
