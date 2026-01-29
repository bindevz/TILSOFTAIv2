namespace TILSOFTAI.Orchestration.Tools;

public sealed class NamedToolHandlerRegistry : INamedToolHandlerRegistry
{
    private readonly Dictionary<string, Type> _handlers = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string toolName, Type handlerType)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            throw new ArgumentException("Tool name is required.", nameof(toolName));
        }

        if (handlerType is null)
        {
            throw new ArgumentNullException(nameof(handlerType));
        }

        if (!typeof(IToolHandler).IsAssignableFrom(handlerType))
        {
            throw new ArgumentException("Handler type must implement IToolHandler.", nameof(handlerType));
        }

        if (_handlers.ContainsKey(toolName))
        {
            throw new InvalidOperationException($"Tool handler for '{toolName}' is already registered.");
        }

        _handlers[toolName] = handlerType;
    }

    public bool TryGet(string toolName, out Type? handlerType)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            handlerType = null;
            return false;
        }

        return _handlers.TryGetValue(toolName, out handlerType);
    }
}
