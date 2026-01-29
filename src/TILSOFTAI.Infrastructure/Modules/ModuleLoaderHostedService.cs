using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Infrastructure.Modules;

public sealed class ModuleLoaderHostedService : IHostedService
{
    private readonly IModuleLoader _moduleLoader;
    private readonly IOptions<ModulesOptions> _modulesOptions;

    public ModuleLoaderHostedService(IModuleLoader moduleLoader, IOptions<ModulesOptions> modulesOptions)
    {
        _moduleLoader = moduleLoader ?? throw new ArgumentNullException(nameof(moduleLoader));
        _modulesOptions = modulesOptions ?? throw new ArgumentNullException(nameof(modulesOptions));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _moduleLoader.LoadModules(_modulesOptions.Value.Enabled);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
