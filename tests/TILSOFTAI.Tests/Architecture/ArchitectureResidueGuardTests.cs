using FluentAssertions;
using System.Text;
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

    [Fact]
    public void ForwardFacingText_ShouldNotContainVisibleMojibake()
    {
        var repositoryRoot = FindRepositoryRoot();
        var forbidden = new[]
        {
            "\u00c3",
            "\u00c2",
            "\u00c4",
            "\u00c6",
            "\u00e2\u20ac",
            "\u00e2\u0153",
            "\u00e2\u2020",
            "\u00e1\u00ba"
        };

        var offenders = EnumerateForwardFacingTextFiles(repositoryRoot)
            .SelectMany(path => forbidden
                .Where(token => File.ReadAllText(path, Encoding.UTF8).Contains(token, StringComparison.Ordinal))
                .Select(token => $"{Path.GetRelativePath(repositoryRoot, path)} contains mojibake token U+{(int)token[0]:X4}"))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        offenders.Should().BeEmpty("forward-facing docs, SQL, CI, and source text should render as clean UTF-8");
    }

    [Fact]
    public void ForwardFacingText_ShouldNotUseUtf8Bom()
    {
        var repositoryRoot = FindRepositoryRoot();
        var offenders = EnumerateForwardFacingTextFiles(repositoryRoot)
            .Where(path =>
            {
                var bytes = File.ReadAllBytes(path);
                return bytes.Length >= 3
                    && bytes[0] == 0xEF
                    && bytes[1] == 0xBB
                    && bytes[2] == 0xBF;
            })
            .Select(path => Path.GetRelativePath(repositoryRoot, path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        offenders.Should().BeEmpty("forward-facing repository text should avoid BOM noise in reviews and tooling");
    }

    [Fact]
    public void PrimaryDocs_ShouldNotHaveStaleSprintHeaders()
    {
        var repositoryRoot = FindRepositoryRoot();
        var docsToScan = new[]
        {
            Path.Combine(repositoryRoot, "docs", "architecture_v3.md"),
            Path.Combine(repositoryRoot, "docs", "compatibility_debt_report.md"),
            Path.Combine(repositoryRoot, "docs", "module_package_classification.md"),
            Path.Combine(repositoryRoot, "docs", "sql_capability_scope_migration.md")
        };
        var staleHeaders = new[] { "Sprint 19", "Sprint 20", "Sprint 21" };

        var offenders = docsToScan
            .Where(File.Exists)
            .Select(path => new { Path = path, Header = File.ReadLines(path, Encoding.UTF8).FirstOrDefault() ?? string.Empty })
            .Where(item => staleHeaders.Any(marker => item.Header.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            .Select(item => $"{Path.GetRelativePath(repositoryRoot, item.Path)} starts with {item.Header}")
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        offenders.Should().BeEmpty("primary docs should describe the current repository state rather than a previous sprint as the current label");
    }

    [Fact]
    public void SqlCompatibilityObservability_ShouldRemainAvailable()
    {
        var repositoryRoot = FindRepositoryRoot();
        var observabilitySql = Path.Combine(repositoryRoot, "sql", "01_core", "082_tables_sql_compatibility_observability.sql");
        var contents = File.ReadAllText(observabilitySql, Encoding.UTF8);

        contents.Should().Contain("SqlCompatibilityUsageLog");
        contents.Should().Contain("SqlCompatibilityUsageDaily");
        contents.Should().Contain("SqlCompatibilityUsageRollup");
        contents.Should().Contain("app_sql_compatibility_usage_summary");
        contents.Should().Contain("app_sql_compatibility_retirement_readiness");
        contents.Should().Contain("app_sql_compatibility_usage_rollup");
        contents.Should().Contain("app_sql_compatibility_usage_purge");
        contents.Should().Contain("SurfaceKind IN (N'legacy-procedure', N'capability-scope-wrapper')");

        var instrumentedSql = new[]
        {
            Path.Combine(repositoryRoot, "sql", "01_core", "071_sps_module_scope.sql"),
            Path.Combine(repositoryRoot, "sql", "01_core", "075_sps_app_policy.sql"),
            Path.Combine(repositoryRoot, "sql", "01_core", "076_sps_app_react_followup.sql"),
            Path.Combine(repositoryRoot, "sql", "01_core", "080_sps_capability_scope_compat.sql"),
            Path.Combine(repositoryRoot, "sql", "97_legacy_diagnostics", "078_tables_module_runtime_catalog.sql")
        };

        var missingInstrumentation = instrumentedSql
            .Where(path => !File.ReadAllText(path, Encoding.UTF8).Contains("app_sql_compatibility_usage_record", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(repositoryRoot, path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        missingInstrumentation.Should().BeEmpty("legacy SQL compatibility paths and forward wrappers should emit retirement-readiness telemetry");
    }

    [Fact]
    public void LegacyRuntimeDiagnosticSql_ShouldBeOptionalNotCore()
    {
        var repositoryRoot = FindRepositoryRoot();
        var corePath = Path.Combine(repositoryRoot, "sql", "01_core", "078_tables_module_runtime_catalog.sql");
        var optionalPath = Path.Combine(repositoryRoot, "sql", "97_legacy_diagnostics", "078_tables_module_runtime_catalog.sql");

        File.Exists(corePath)
            .Should().BeFalse("ModuleRuntimeCatalog should not remain in the default core SQL deployment path");
        File.Exists(optionalPath)
            .Should().BeTrue("historical package-runtime diagnostics should be explicitly optional while retirement evidence is gathered");

        var contents = File.ReadAllText(optionalPath, Encoding.UTF8);
        contents.Should().Contain("OPTIONAL LEGACY DIAGNOSTICS");
        contents.Should().Contain("normal core");
        contents.Should().Contain("app_sql_compatibility_usage_record");
    }

    [Fact]
    public void CompatibilityInventory_ShouldBoundRemainingLegacyEnvelope()
    {
        var repositoryRoot = FindRepositoryRoot();
        var inventoryPath = Path.Combine(repositoryRoot, "docs", "compatibility_inventory.json");
        using var document = JsonDocument.Parse(File.ReadAllText(inventoryPath, Encoding.UTF8));
        var root = document.RootElement;

        root.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        root.GetProperty("inventoryVersion").GetString().Should().Be("sprint-23");

        ReadNames(root.GetProperty("physicalStorageNames"))
            .Should().BeEquivalentTo(
                "ModuleCatalog",
                "ToolCatalogScope.ModuleKey",
                "MetadataDictionaryScope.ModuleKey",
                "RuntimePolicy.ModuleKey",
                "ReActFollowUpRule.ModuleKey");

        ReadNames(root.GetProperty("legacyProcedures"))
            .Should().BeEquivalentTo(
                "app_modulecatalog_list",
                "app_toolcatalog_list_scoped",
                "app_metadatadictionary_list_scoped",
                "app_policy_resolve",
                "app_react_followup_list_scoped");

        var diagnostics = root.GetProperty("legacyDiagnostics");
        diagnostics.GetArrayLength().Should().Be(1);
        diagnostics[0].GetProperty("deploymentPath").GetString()
            .Should().Be("sql/97_legacy_diagnostics/078_tables_module_runtime_catalog.sql");
        diagnostics[0].GetProperty("defaultDeployment").GetBoolean().Should().BeFalse();

        foreach (var path in ReadInventoryPaths(root))
        {
            File.Exists(Path.Combine(repositoryRoot, path))
                .Should().BeTrue($"compatibility inventory path should exist: {path}");
        }
    }

    [Fact]
    public void DbMajorEvidencePacketTemplate_ShouldContainReleaseDecisionInputs()
    {
        var repositoryRoot = FindRepositoryRoot();
        var templatePath = Path.Combine(repositoryRoot, "docs", "db_major_readiness_evidence_packet.template.json");
        using var document = JsonDocument.Parse(File.ReadAllText(templatePath, Encoding.UTF8));
        var root = document.RootElement;

        root.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        root.GetProperty("packetType").GetString().Should().Be("db-major-compatibility-retirement-readiness");
        root.GetProperty("compatibilityInventory").GetProperty("inventoryPath").GetString()
            .Should().Be("docs/compatibility_inventory.json");
        root.GetProperty("telemetryWindow").TryGetProperty("legacyProcedureUsageCount", out _)
            .Should().BeTrue();
        root.GetProperty("telemetryWindow").TryGetProperty("capabilityScopeWrapperUsageCount", out _)
            .Should().BeTrue();
        root.GetProperty("readinessDecision").TryGetProperty("isDbMajorRenameCandidate", out _)
            .Should().BeTrue();
        root.GetProperty("releaseAttachments").TryGetProperty("rollbackPlanUri", out _)
            .Should().BeTrue();
        root.GetProperty("releaseAttachments").TryGetProperty("fallbackPostureUri", out _)
            .Should().BeTrue();
        root.GetProperty("fallbackPosture").TryGetProperty("catalogSourceMode", out _)
            .Should().BeTrue();
    }

    [Fact]
    public void ReleaseEvidenceAutomation_ShouldRemainExecutableAndValidated()
    {
        var repositoryRoot = FindRepositoryRoot();
        var certificationGenerator = Path.Combine(repositoryRoot, "tools", "evidence", "New-CertificationRunManifest.ps1");
        var certificationValidator = Path.Combine(repositoryRoot, "tools", "evidence", "Test-CertificationRunManifest.ps1");
        var generator = Path.Combine(repositoryRoot, "tools", "evidence", "New-ReleaseEvidenceBundle.ps1");
        var validator = Path.Combine(repositoryRoot, "tools", "evidence", "Test-ReleaseEvidenceBundle.ps1");
        var summaryGenerator = Path.Combine(repositoryRoot, "tools", "evidence", "New-CertificationReviewSummary.ps1");
        var bundleDocs = Path.Combine(repositoryRoot, "docs", "release_evidence_bundles.md");
        var executionDocs = Path.Combine(repositoryRoot, "docs", "staging_prodlike_certification_execution.md");
        var signingDecision = Path.Combine(repositoryRoot, "docs", "signed_artifact_verification_decision.md");
        var runTemplate = Path.Combine(repositoryRoot, "docs", "certification_run_manifest.template.json");
        var evidenceRefs = Path.Combine(repositoryRoot, "docs", "certification_evidence_refs.example.json");

        File.Exists(certificationGenerator).Should().BeTrue("Sprint 25 requires a first-class certification run manifest generator");
        File.Exists(certificationValidator).Should().BeTrue("Sprint 25 requires stricter certification evidence validation");
        File.Exists(generator).Should().BeTrue("Sprint 24 requires an executable evidence generation flow");
        File.Exists(validator).Should().BeTrue("Sprint 24 requires generated evidence validation");
        File.Exists(summaryGenerator).Should().BeTrue("Sprint 25 requires a certification review summary generator");
        File.Exists(bundleDocs).Should().BeTrue("operators need the bundle convention");
        File.Exists(executionDocs).Should().BeTrue("operators need one staging/prod-like certification execution path");
        File.Exists(signingDecision).Should().BeTrue("signed artifact verification must be explicitly scoped or deferred");
        File.Exists(runTemplate).Should().BeTrue("certification run manifests should have a stable machine-readable template");

        var certificationGeneratorText = File.ReadAllText(certificationGenerator, Encoding.UTF8);
        certificationGeneratorText.Should().Contain("fallbackPosture");
        certificationGeneratorText.Should().Contain("requiredEvidence");
        certificationGeneratorText.Should().Contain("blocked_example");

        var certificationValidatorText = File.ReadAllText(certificationValidator, Encoding.UTF8);
        certificationValidatorText.Should().Contain("unsupported URI or identifier");
        certificationValidatorText.Should().Contain("Operator signoff evidence is required");
        certificationValidatorText.Should().Contain("Production-like fallback requires fallbackAuthorizationUri");

        var generatorText = File.ReadAllText(generator, Encoding.UTF8);
        generatorText.Should().Contain("CertificationRunPath");
        generatorText.Should().Contain("compatibility_inventory.json");
        generatorText.Should().Contain("fallback-posture.json");
        generatorText.Should().Contain("certification-evidence-manifest.json");
        generatorText.Should().Contain("Get-FileHash");

        var validatorText = File.ReadAllText(validator, Encoding.UTF8);
        validatorText.Should().Contain("Missing certification evidence");
        validatorText.Should().Contain("Production-like fallback was used without authorization evidence");

        var summaryGeneratorText = File.ReadAllText(summaryGenerator, Encoding.UTF8);
        summaryGeneratorText.Should().Contain("certification-review-summary.json");
        summaryGeneratorText.Should().Contain("missing_evidence");
        summaryGeneratorText.Should().Contain("fallback_authorization_gap");

        using var templateDocument = JsonDocument.Parse(File.ReadAllText(runTemplate, Encoding.UTF8));
        var templateRoot = templateDocument.RootElement;
        templateRoot.GetProperty("manifestType").GetString().Should().Be("staging-prodlike-certification-run");
        templateRoot.GetProperty("fallbackPosture").TryGetProperty("fallbackDecision", out _).Should().BeTrue();
        ReadEvidenceKinds(templateRoot.GetProperty("requiredEvidence")).Should().BeEquivalentTo(RequiredCertificationEvidenceKinds);

        using var refsDocument = JsonDocument.Parse(File.ReadAllText(evidenceRefs, Encoding.UTF8));
        var refsRoot = refsDocument.RootElement;
        foreach (var evidenceKind in RequiredCertificationEvidenceKinds)
        {
            refsRoot.TryGetProperty(evidenceKind, out var value).Should().BeTrue($"{evidenceKind} should have an example evidence reference");
            value.GetString().Should().StartWith("artifact://");
        }
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

        return Path.GetExtension(path) is ".cs" or ".csproj" or ".json" or ".md" or ".sql" or ".slnx" or ".yml" or ".yaml" or ".ps1";
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

    private static IEnumerable<string> EnumerateForwardFacingTextFiles(string repositoryRoot)
    {
        var directories = new[]
        {
            ".github",
            "docs",
            "sql",
            "src",
            "tests",
            "tools"
        };

        foreach (var directoryName in directories)
        {
            var directory = Path.Combine(repositoryRoot, directoryName);
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Where(ShouldScan))
            {
                yield return file;
            }
        }

        var rootFiles = new[]
        {
            Path.Combine(repositoryRoot, "README.md"),
            Path.Combine(repositoryRoot, "TILSOFTAI.slnx")
        };

        foreach (var file in rootFiles.Where(File.Exists))
        {
            yield return file;
        }
    }

    private static string[] ReadNames(JsonElement array)
    {
        return array
            .EnumerateArray()
            .Select(item => item.GetProperty("name").GetString() ?? string.Empty)
            .ToArray();
    }

    private static IEnumerable<string> ReadInventoryPaths(JsonElement root)
    {
        foreach (var sectionName in new[] { "physicalStorageNames", "legacyProcedures", "forwardWrappers" })
        {
            foreach (var item in root.GetProperty(sectionName).EnumerateArray())
            {
                foreach (var path in item.GetProperty("repoPaths").EnumerateArray())
                {
                    yield return path.GetString() ?? string.Empty;
                }
            }
        }

        foreach (var item in root.GetProperty("legacyDiagnostics").EnumerateArray())
        {
            yield return item.GetProperty("deploymentPath").GetString() ?? string.Empty;
        }
    }

    private static string[] ReadEvidenceKinds(JsonElement array)
    {
        return array
            .EnumerateArray()
            .Select(item => item.GetProperty("evidenceKind").GetString() ?? string.Empty)
            .ToArray();
    }

    private static readonly string[] RequiredCertificationEvidenceKinds =
    {
        "runbook_execution",
        "preview_failure_drill",
        "version_conflict_drill",
        "duplicate_submit_drill",
        "sql_apply_outage_drill",
        "fallback_risk_drill",
        "operator_signoff"
    };
}
