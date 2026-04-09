using Microsoft.Extensions.Logging;

namespace TILSOFTAI.Agents.Domain;

/// <summary>
/// Sprint 2 accounting domain agent skeleton.
/// Routes to accounting domain; delegates execution via LegacyChatPipelineBridge.
/// Does not own specific tool prefixes yet (Option 4).
/// </summary>
public sealed class AccountingAgent : DomainAgentBase
{
    public AccountingAgent(LegacyChatPipelineBridge bridge, ILogger<AccountingAgent> logger)
        : base(bridge, logger)
    {
    }

    public override string AgentId => "accounting";
    public override string DisplayName => "Accounting Domain Agent";
    public override IReadOnlyList<string> OwnedDomains { get; } = new[] { "accounting" };
}
