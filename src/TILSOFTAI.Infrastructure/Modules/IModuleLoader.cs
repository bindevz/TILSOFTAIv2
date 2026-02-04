using System.Reflection;
using TILSOFTAI.Orchestration.Modules;

namespace TILSOFTAI.Infrastructure.Modules;

public interface IModuleLoader
{
    IReadOnlyList<Assembly> LoadedModules { get; }
    void LoadModules(IEnumerable<string> moduleNames);
    IReadOnlyList<ITilsoftModule> GetLoadedModules();
}
