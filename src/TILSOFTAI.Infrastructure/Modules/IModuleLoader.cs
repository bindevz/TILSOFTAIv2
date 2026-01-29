using System.Reflection;

namespace TILSOFTAI.Infrastructure.Modules;

public interface IModuleLoader
{
    IReadOnlyList<Assembly> LoadedModules { get; }
    void LoadModules(IEnumerable<string> moduleNames);
}
