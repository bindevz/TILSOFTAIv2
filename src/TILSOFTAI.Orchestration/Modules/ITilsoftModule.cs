using TILSOFTAI.Orchestration.Tools;

namespace TILSOFTAI.Orchestration.Modules;

public interface ITilsoftModule
{
    string Name { get; }
    void Register(IToolRegistry toolRegistry, INamedToolHandlerRegistry handlerRegistry);
}
