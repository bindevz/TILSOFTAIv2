namespace TILSOFTAI.Orchestration.Policies;

/// <summary>
/// Domain model for a ReAct follow-up rule loaded from SQL.
/// These rules define conditions under which the LLM should be nudged
/// to call a follow-up tool after a trigger tool returns data.
/// </summary>
public sealed record ReActFollowUpRule(
    long RuleId,
    string RuleKey,
    string ModuleKey,
    string? ToolName,
    int Priority,
    string JsonPath,
    string Operator,
    string? CompareValue,
    string FollowUpToolName,
    string? ArgsTemplateJson,
    string PromptHint);
