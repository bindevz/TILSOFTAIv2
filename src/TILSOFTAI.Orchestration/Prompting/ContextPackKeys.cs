namespace TILSOFTAI.Orchestration.Prompting;

/// <summary>
/// PATCH 36.06: Canonical context pack key constants.
/// All providers and the budgeter reference these instead of local strings.
/// </summary>
public static class ContextPackKeys
{
    public const string ToolCatalog = "tool_catalog";
    public const string AtomicCatalog = "atomic_catalog";
    public const string MetadataDictionary = "metadata_dictionary";
    public const string ReactFollowUpRules = "react_followup_rules";
}
