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
            throw new ArgumentException("ToolDefinition.Name is required.", nameof(def));
        }

        if (string.IsNullOrWhiteSpace(def.Instruction))
        {
            throw new ArgumentException("ToolDefinition.Instruction is required.", nameof(def));
        }

        if (string.IsNullOrWhiteSpace(def.JsonSchema))
        {
            throw new ArgumentException("ToolDefinition.JsonSchema is required.", nameof(def));
        }

        if (!string.IsNullOrWhiteSpace(def.SpName) && !def.SpName.StartsWith("ai_", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("ToolDefinition.SpName must start with 'ai_'.", nameof(def));
        }

        if (_definitions.ContainsKey(def.Name))
        {
            throw new InvalidOperationException($"Tool '{def.Name}' is already registered.");
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
            throw new ArgumentException("Tool name is required.", nameof(name));
        }

        if (!_definitions.TryGetValue(name, out var def))
        {
            throw new KeyNotFoundException($"Tool '{name}' is not registered.");
        }

        return def;
    }
}
