using System.Reflection;
using TILSOFTAI.Orchestration.Modules;

namespace TILSOFTAI.Infrastructure.Modules;

[Obsolete("Reflection-driven module loading is deprecated in Sprint 1. Plan migration to capability-pack loading.")]
public interface IModuleLoader
{
    IReadOnlyList<Assembly> LoadedModules { get; }
    void LoadModules(IEnumerable<string> moduleNames);
    IReadOnlyList<ITilsoftModule> GetLoadedModules();
}
