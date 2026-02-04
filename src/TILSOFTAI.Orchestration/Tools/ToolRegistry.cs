using TILSOFTAI.Domain.Properties;

namespace TILSOFTAI.Orchestration.Tools;

public sealed class ToolRegistry : IToolRegistry
{
    private readonly Dictionary<string, ToolDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);

    public void Register(ToolDefinition def)
    {
        if (def is null)
        {
            throw new ArgumentNullException(nameof(def));
        }

        if (string.IsNullOrWhiteSpace(def.Name))
        {
            throw new ArgumentException(Resources.Val_ToolDefinitionNameRequired, nameof(def));
        }

        if (string.IsNullOrWhiteSpace(def.Instruction))
        {
            throw new ArgumentException(Resources.Val_ToolDefinitionInstructionRequired, nameof(def));
        }

        if (string.IsNullOrWhiteSpace(def.JsonSchema))
        {
            throw new ArgumentException(Resources.Val_ToolDefinitionJsonSchemaRequired, nameof(def));
        }

        if (!string.IsNullOrWhiteSpace(def.SpName) && !def.SpName.StartsWith("ai_", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(Resources.Val_ToolDefinitionSpNameMustStartWithAi, nameof(def));
        }

        if (_definitions.ContainsKey(def.Name))
        {
            throw new InvalidOperationException(string.Format(Resources.Ex_ToolAlreadyRegistered, def.Name));
        }

        _definitions[def.Name] = def;
    }

    public IReadOnlyList<ToolDefinition> ListEnabled()
    {
        return _definitions.Values.Where(def => def.IsEnabled).ToList();
    }

    public ToolDefinition Get(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(Resources.Val_ToolNameRequired, nameof(name));
        }

        if (!_definitions.TryGetValue(name, out var def))
        {
            throw new KeyNotFoundException($"Tool '{name}' is not registered.");
        }

        return def;
    }
}
