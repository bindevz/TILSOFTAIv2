using TILSOFTAI.Orchestration.Modules;
using Xunit;

namespace TILSOFTAI.Tests.Modules;

/// <summary>
/// PATCH 37.05: Module activation tests for DB-first loading and fallback.
/// Tests only use Domain/Orchestration types (no Infrastructure dependency in test project).
/// </summary>
public sealed class ModuleActivationTests
{
    [Fact]
    public void ModulesOptions_FallbackEnabled_DefaultsToPlatform()
    {
        var options = new Domain.Configuration.ModulesOptions();

        Assert.Single(options.FallbackEnabled);
        Assert.Equal("TILSOFTAI.Modules.Platform", options.FallbackEnabled[0]);
    }

    [Fact]
    public void ModulesOptions_Enabled_DefaultsToEmpty()
    {
        var options = new Domain.Configuration.ModulesOptions();
        Assert.Empty(options.Enabled);
    }

    [Fact]
    public void IModuleActivationProvider_InterfaceContract()
    {
        // Verify the interface has the expected method signature
        var method = typeof(IModuleActivationProvider).GetMethod("GetEnabledModulesAsync");
        Assert.NotNull(method);
        Assert.Equal(3, method!.GetParameters().Length);
    }

    [Fact]
    public void IModuleActivationProvider_ReturnsExpectedType()
    {
        var method = typeof(IModuleActivationProvider).GetMethod("GetEnabledModulesAsync");
        Assert.NotNull(method);

        // Return type is Task<IReadOnlyList<string>>
        var returnType = method!.ReturnType;
        Assert.True(returnType.IsGenericType);
        Assert.Equal(typeof(System.Threading.Tasks.Task<>), returnType.GetGenericTypeDefinition());
    }
}
