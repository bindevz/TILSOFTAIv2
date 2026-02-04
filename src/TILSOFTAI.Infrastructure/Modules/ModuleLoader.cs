using System.Reflection;
using Microsoft.Extensions.Logging;
using TILSOFTAI.Orchestration.Modules;
using TILSOFTAI.Orchestration.Tools;

namespace TILSOFTAI.Infrastructure.Modules;

public sealed class ModuleLoader : IModuleLoader
{
    private readonly ILogger<ModuleLoader> _logger;
    private readonly IToolRegistry _toolRegistry;
    private readonly INamedToolHandlerRegistry _handlerRegistry;
    private readonly List<Assembly> _loaded = new();
    private readonly List<ITilsoftModule> _loadedModules = new();

    public ModuleLoader(
        ILogger<ModuleLoader> logger,
        IToolRegistry toolRegistry,
        INamedToolHandlerRegistry handlerRegistry)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _handlerRegistry = handlerRegistry ?? throw new ArgumentNullException(nameof(handlerRegistry));
    }

    public IReadOnlyList<Assembly> LoadedModules => _loaded;

    public void LoadModules(IEnumerable<string> moduleNames)
    {
        foreach (var moduleName in moduleNames.Where(name => !string.IsNullOrWhiteSpace(name)))
        {
            try
            {
                var assembly = Assembly.Load(moduleName);
                _loaded.Add(assembly);
                _logger.LogInformation("Loaded module assembly {ModuleName}.", moduleName);
                RegisterModules(assembly);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load module assembly {ModuleName}.", moduleName);
            }
        }
    }

    private void RegisterModules(Assembly assembly)
    {
        Type[] types;

        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t is not null).Cast<Type>().ToArray();
        }

        foreach (var type in types)
        {
            if (type.IsAbstract || !typeof(ITilsoftModule).IsAssignableFrom(type))
            {
                continue;
            }

            if (Activator.CreateInstance(type) is not ITilsoftModule module)
            {
                continue;
            }

            module.Register(_toolRegistry, _handlerRegistry);
            _loadedModules.Add(module);
            _logger.LogInformation("Registered module {ModuleName} from {AssemblyName}.", module.Name, assembly.GetName().Name);
        }
    }

    public IReadOnlyList<ITilsoftModule> GetLoadedModules()
    {
        return _loadedModules.AsReadOnly();
    }
}
