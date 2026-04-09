using TILSOFTAI.Orchestration.Modules;
using TILSOFTAI.Orchestration.Tools;

namespace TILSOFTAI.Orchestration.Capabilities;

/// <summary>
/// Sprint 2 bridge: wraps an existing ITilsoftModule as an ICapabilityPackProvider.
/// Once module-centric loading is fully replaced, this wrapper can be removed.
/// </summary>
public sealed class ModuleBackedCapabilityPack : ICapabilityPackProvider
{
    private readonly ITilsoftModule _module;

    public ModuleBackedCapabilityPack(ITilsoftModule module, IReadOnlyList<string>? domainAffinity = null)
    {
        _module = module ?? throw new ArgumentNullException(nameof(module));
        DomainAffinity = domainAffinity ?? Array.Empty<string>();
    }

    public string PackName => _module.Name;

    public IReadOnlyList<string> DomainAffinity { get; }

    public void RegisterTools(IToolRegistry registry, INamedToolHandlerRegistry handlerRegistry)
    {
        _module.Register(registry, handlerRegistry);
    }
}
