namespace TILSOFTAI.Orchestration.Tools;

public interface IToolRegistry
{
    void Register(ToolDefinition def);
    IReadOnlyList<ToolDefinition> ListEnabled();
    ToolDefinition Get(string name);
}
