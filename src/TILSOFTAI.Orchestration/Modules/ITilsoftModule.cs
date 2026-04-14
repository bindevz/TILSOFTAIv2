using TILSOFTAI.Orchestration.Tools;

namespace TILSOFTAI.Orchestration.Modules;

/// <summary>
/// Legacy package contract retained only so solution-local compatibility packages compile.
/// Production API startup does not load modules.
/// </summary>
public interface ITilsoftModule
{
    string Name { get; }
    void Register(IToolRegistry toolRegistry, INamedToolHandlerRegistry handlerRegistry);
}
