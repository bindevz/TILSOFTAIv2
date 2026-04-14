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
    public void Solution_ShouldNotContainRetiredPackageShellProjects()
    {
        var repositoryRoot = FindRepositoryRoot();
        var solution = Path.Combine(repositoryRoot, "TILSOFTAI.slnx");
        var contents = File.ReadAllText(solution);

        contents.Should().NotContainAny(
            new[] { "TILSOFTAI.Modules.Platform", "TILSOFTAI.Modules.Analytics" },
            "Sprint 21 retired the residual Platform and Analytics package shells from the solution");

        Directory.Exists(Path.Combine(repositoryRoot, "src", "TILSOFTAI.Modules.Platform"))
            .Should().BeFalse("the Platform package shell should not remain as ambiguous future-facing residue");
        Directory.Exists(Path.Combine(repositoryRoot, "src", "TILSOFTAI.Modules.Analytics"))
            .Should().BeFalse("the Analytics package shell should not remain as ambiguous future-facing residue");
        Directory.Exists(Path.Combine(repositoryRoot, "sql", "90_template_module"))
            .Should().BeFalse("new SQL templates should not normalize module-era naming");
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
            "IModuleActivationProvider",
            "ITilsoftModule"
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

    [Fact]
    public void RuntimeCode_ShouldUseCapabilityScopeSqlNames()
    {
        var repositoryRoot = FindRepositoryRoot();
        var forbidden = new[]
        {
            "@ModuleKeysJson",
            "@ModulesJson",
            "app_toolcatalog_list_scoped",
            "app_metadatadictionary_list_scoped",
            "app_policy_resolve\"",
            "app_react_followup_list_scoped"
        };

        var offenders = Directory
            .EnumerateFiles(Path.Combine(repositoryRoot, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(ShouldScan)
            .SelectMany(path => forbidden
                .Where(token => File.ReadAllText(path).Contains(token, StringComparison.Ordinal))
                .Select(token => $"{Path.GetRelativePath(repositoryRoot, path)} contains {token}"))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        offenders.Should().BeEmpty("Sprint 21 moved runtime callers to capability-scope SQL wrappers");
    }

    [Fact]
    public void ForwardLookingDocs_ShouldNotNormalizeModuleRuntimeOwnership()
    {
        var repositoryRoot = FindRepositoryRoot();
        var docsToScan = new[]
        {
            Path.Combine(repositoryRoot, "README.md"),
            Path.Combine(repositoryRoot, "docs", "architecture_v3.md"),
            Path.Combine(repositoryRoot, "docs", "runtime_readiness.md"),
            Path.Combine(repositoryRoot, "docs", "module_package_classification.md"),
            Path.Combine(repositoryRoot, "src", "TILSOFTAI.Api", "appsettings.Sample.README.md")
        };
        var forbidden = new[]
        {
            "Modules:EnableLegacyAutoload",
            "ModuleHealthCheck",
            "ModuleLoaderHostedService",
            "module loader remains",
            "module packages are retained"
        };

        var offenders = docsToScan
            .Where(File.Exists)
            .SelectMany(path => forbidden
                .Where(token => File.ReadAllText(path).Contains(token, StringComparison.OrdinalIgnoreCase))
                .Select(token => $"{Path.GetRelativePath(repositoryRoot, path)} contains {token}"))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        offenders.Should().BeEmpty("forward-looking docs should describe capability/catalog ownership, not normalize module runtime ownership");
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
