using TILSOFTAI.Orchestration.Semantic;

namespace TILSOFTAI.Orchestration.AiRouting.MicrosoftAgentFramework;

public static class AgentInstructionsBuilder
{
    public static string Build(
        AgentToolRoutingRequest request,
        HardSignalSet hardSignals,
        CapabilityRetrievalResult retrieval)
    {
        var domains = retrieval.Domains.Count == 0
            ? "none"
            : string.Join(", ", retrieval.Domains.Select(domain => $"{domain.Domain}:{domain.Score:0.00}"));

        var roleSummary = request.ExecutionContext.Roles.Length == 0
            ? "none"
            : string.Join(", ", request.ExecutionContext.Roles.Take(10));

        var keywords = hardSignals.BusinessKeywords.Count == 0
            ? "none"
            : string.Join(", ", hardSignals.BusinessKeywords.Take(12));

        return $"""
You are an internal ERP assistant.

Rules:
- Use only the provided ERP function tools.
- Never generate SQL.
- Never invent item codes, customer codes, supplier codes, warehouse IDs, invoice numbers, PO numbers, or SO numbers.
- If required parameters are missing, ask a concise clarification question.
- If multiple entity candidates are possible, ask the user to choose.
- For create/update/delete/post/approve/cancel actions, only use preview tools unless an approved action is explicitly provided by the system.
- Do not mention stored procedure names unless debug mode is enabled.
- Reply in the user's locale unless system configuration says otherwise.

Locale: {request.Locale}
Tenant: {request.ExecutionContext.TenantId}
User roles: {roleSummary}
Candidate domains: {domains}
Candidate tool count: {retrieval.Capabilities.Count}
Hard-signal keywords: {keywords}
Current business date/time: {DateTimeOffset.UtcNow:O}
""";
    }
}
