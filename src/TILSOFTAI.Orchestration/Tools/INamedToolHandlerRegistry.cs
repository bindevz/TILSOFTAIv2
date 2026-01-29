namespace TILSOFTAI.Orchestration.Tools;

public interface INamedToolHandlerRegistry
{
    void Register(string toolName, Type handlerType);
    bool TryGet(string toolName, out Type? handlerType);
}
