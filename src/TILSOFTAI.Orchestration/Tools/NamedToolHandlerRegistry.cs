using TILSOFTAI.Domain.Properties;

namespace TILSOFTAI.Orchestration.Tools;

public sealed class NamedToolHandlerRegistry : INamedToolHandlerRegistry
{
    private readonly Dictionary<string, Type> _handlers = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string toolName, Type handlerType)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            throw new ArgumentException(Resources.Val_ToolNameRequired, nameof(toolName));
        }

        if (handlerType is null)
        {
            throw new ArgumentNullException(nameof(handlerType));
        }

        if (!typeof(IToolHandler).IsAssignableFrom(handlerType))
        {
            throw new ArgumentException(Resources.Val_HandlerTypeMustImplementIToolHandler, nameof(handlerType));
        }

        if (_handlers.ContainsKey(toolName))
        {
            var existingType = _handlers[toolName].FullName ?? _handlers[toolName].Name;
            throw new InvalidOperationException(
                string.Format(Resources.Ex_ToolAlreadyRegisteredWithDifferentHandler, toolName, existingType));
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
