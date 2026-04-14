using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace TILSOFTAI.Tests.Architecture;

public sealed class ArchitectureResidueGuardTests
{
    [Fact]
    public void Repository_ShouldNotReintroduceRemovedModelModuleIdentity()
    {
        var repositoryRoot = FindRepositoryRoot();
        var forbidden = string.Concat("TILSOFTAI.Modules", ".Model");
        var offenders = Directory
            .EnumerateFiles(repositoryRoot, "*", SearchOption.AllDirectories)
            .Where(ShouldScanSource)
            .Where(path => File.ReadAllText(path).Contains(forbidden, StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(repositoryRoot, path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        offenders.Should().BeEmpty("Sprint 19 removed the Model module as a supported project and ownership concept");
    }

    [Fact]
    public void ApiProject_ShouldNotReferenceLegacyPackageProjects()
    {
        var repositoryRoot = FindRepositoryRoot();
        var apiProject = Path.Combine(repositoryRoot, "src", "TILSOFTAI.Api", "TILSOFTAI.Api.csproj");
        var contents = File.ReadAllText(apiProject);

        contents.Should().NotContainAny(
            new[] { "TILSOFTAI.Modules.Platform", "TILSOFTAI.Modules.Analytics" },
            "Sprint 20 removed Platform and Analytics packages from the production API project graph");
    }

    [Fact]
    public void ApiSettings_ShouldNotContainModulesSection()
    {
        var repositoryRoot = FindRepositoryRoot();
        var appsettings = Path.Combine(repositoryRoot, "src", "TILSOFTAI.Api", "appsettings.json");

        using var document = JsonDocument.Parse(File.ReadAllText(appsettings));

        document.RootElement.TryGetProperty("Modules", out _)
            .Should().BeFalse("Sprint 20 removed the default runtime Modules configuration section");
    }

    [Fact]
    public void ApiRuntime_ShouldNotRegisterLegacyModuleSubstrate()
    {
        var repositoryRoot = FindRepositoryRoot();
        var apiRoot = Path.Combine(repositoryRoot, "src", "TILSOFTAI.Api");
        var forbidden = new[]
        {
            "ModuleLoaderHostedService",
            "ModuleHealthCheck",
            "Modules:EnableLegacyAutoload",
            "IModuleLoader"
        };

        var offenders = Directory
            .EnumerateFiles(apiRoot, "*", SearchOption.AllDirectories)
            .Where(ShouldScan)
            .SelectMany(path => forbidden
                .Where(token => File.ReadAllText(path).Contains(token, StringComparison.Ordinal))
                .Select(token => $"{Path.GetRelativePath(repositoryRoot, path)} contains {token}"))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        offenders.Should().BeEmpty("Sprint 20 retired module loader, module health, and module autoload config from API runtime");
    }

    [Fact]
    public void Repository_ShouldNotReintroduceLegacyModuleScopeResolver()
    {
        var repositoryRoot = FindRepositoryRoot();
        var forbidden = new[]
        {
            "IModuleScopeResolver",
            "ModuleScopeResolver",
            "ModuleScopeResult",
            "IModuleActivationProvider"
        };

        var offenders = Directory
            .EnumerateFiles(repositoryRoot, "*", SearchOption.AllDirectories)
            .Where(ShouldScanSource)
            .SelectMany(path => forbidden
                .Where(token => File.ReadAllText(path).Contains(token, StringComparison.Ordinal))
                .Select(token => $"{Path.GetRelativePath(repositoryRoot, path)} contains {token}"))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        offenders.Should().BeEmpty("Sprint 20 removed the legacy module scope resolver and activation provider");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TILSOFTAI.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }

    private static bool ShouldScan(string path)
    {
        if (Path.GetFileName(path).Equals(nameof(ArchitectureResidueGuardTests) + ".cs", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (segments.Any(segment =>
                segment.Equals(".git", StringComparison.OrdinalIgnoreCase)
                || segment.Equals("bin", StringComparison.OrdinalIgnoreCase)
                || segment.Equals("obj", StringComparison.OrdinalIgnoreCase)
                || segment.Equals("spec", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return Path.GetExtension(path) is ".cs" or ".csproj" or ".json" or ".md" or ".sql" or ".slnx";
    }

    private static bool ShouldScanSource(string path)
    {
        if (!ShouldScan(path))
        {
            return false;
        }

        var relativePath = Path.GetRelativePath(FindRepositoryRoot(), path);
        return relativePath.StartsWith("src" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || relativePath.StartsWith("tests" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
