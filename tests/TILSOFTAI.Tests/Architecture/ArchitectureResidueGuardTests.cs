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
    public void AgentFrameworkRuntime_ShouldNotContainFakeCandidateGatedRouting()
    {
        var repositoryRoot = FindRepositoryRoot();
        var runtimeRoot = Path.Combine(repositoryRoot, "src", "TILSOFTAI.Orchestration", "AiRouting", "MicrosoftAgentFramework");
        var forbidden = new[]
        {
            "AgentClientFactory",
            "IAgentClientFactory",
            "IToolCallingAgent",
            "CreateToolCallingAgent",
            "CandidateGatedToolCallingAgent",
            "_tools.FirstOrDefault()",
            "_tools[0]",
            "ExtractArgumentValue("
        };

        var offenders = Directory
            .EnumerateFiles(runtimeRoot, "*.cs", SearchOption.AllDirectories)
            .Where(ShouldScan)
            .SelectMany(path => forbidden
                .Where(token => File.ReadAllText(path, Encoding.UTF8).Contains(token, StringComparison.Ordinal))
                .Select(token => $"{Path.GetRelativePath(repositoryRoot, path)} contains {token}"))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        offenders.Should().BeEmpty("Sprint 33 phase 0 removes custom tool-calling agents, fake first-candidate routing, and regex-only argument binding from Agent Framework runtime");
    }

    [Fact]
    public void RuntimeDi_ShouldNotResolveCustomAgentBrains()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sourceRoot = Path.Combine(repositoryRoot, "src");
        var forbidden = new[]
        {
            "IAgentClientFactory",
            "IToolCallingAgent",
            "IAgentFunctionToolFactory",
            "AgentFunctionTool",
            "OfficialAgentToolFactory",
            "IOfficialAgentToolFactory",
            "AgentClientFactory",
            "CandidateGatedToolCallingAgent"
        };

        var offenders = Directory
            .EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(ShouldScan)
            .SelectMany(path => forbidden
                .Where(token => File.ReadAllText(path, Encoding.UTF8).Contains(token, StringComparison.Ordinal))
                .Select(token => $"{Path.GetRelativePath(repositoryRoot, path)} contains {token}"))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        offenders.Should().BeEmpty("Sprint 33 phase 0 requires runtime DI to invoke the official agent runtime directly, with no custom agent brain registered as an intermediate selector");
    }

    [Fact]
    public void AgentFrameworkRuntime_ShouldInvokeOfficialMicrosoftAgent()
    {
        var repositoryRoot = FindRepositoryRoot();
        var runtimePath = Path.Combine(
            repositoryRoot,
            "src",
            "TILSOFTAI.Orchestration",
            "AiRouting",
            "MicrosoftAgentFramework",
            "OfficialMicrosoftAgentRuntime.cs");
        var contents = File.ReadAllText(runtimePath, Encoding.UTF8);
        var providerFactory = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "TILSOFTAI.Orchestration", "AiRouting", "MicrosoftAgentFramework", "OfficialAgentProviderFactory.cs"),
            Encoding.UTF8);
        var functionProvider = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "TILSOFTAI.Orchestration", "AiRouting", "Tools", "DynamicFunctionToolFactory.cs"),
            Encoding.UTF8);

        providerFactory.Should().Contain("AsAIAgent", "AgentFramework mode must create agents through the official Microsoft Agent Framework extension");
        contents.Should().Contain("IOfficialAgentProviderFactory", "runtime must get real AIAgent instances from the official provider factory");
        functionProvider.Should().Contain(": AIFunction", "candidate capabilities must be converted to official Microsoft.Extensions.AI function types before model selection");
        functionProvider.Should().Contain("JsonSchema", "official functions should expose SQL-backed parameter schemas to the model");
        functionProvider.Should().Contain("readonly", "SQL-seeded read capabilities must remain model-callable");
        contents.Should().Contain(".RunAsync(", "production routing must invoke the official agent before any tool result can be selected");
    }

    [Fact]
    public void AgentFrameworkSetup_ShouldUseOfficialPackagesAndAvoidLocalImpostorTypes()
    {
        var repositoryRoot = FindRepositoryRoot();
        var orchestrationProject = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "TILSOFTAI.Orchestration", "TILSOFTAI.Orchestration.csproj"),
            Encoding.UTF8);
        var apiProject = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "TILSOFTAI.Api", "TILSOFTAI.Api.csproj"),
            Encoding.UTF8);

        orchestrationProject.Should().Contain("Microsoft.Agents.AI");
        orchestrationProject.Should().Contain("Microsoft.Extensions.AI");
        apiProject.Should().Contain("Microsoft.Extensions.AI.OpenAI");
        apiProject.Should().Contain("Azure.AI.OpenAI");

        var sourceFiles = Directory
            .EnumerateFiles(Path.Combine(repositoryRoot, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(ShouldScan);
        var forbiddenDeclarations = new[]
        {
            "class AIAgent",
            "record AIAgent",
            "class AIFunction",
            "record AIFunction",
            "class AgentResponse",
            "record AgentResponse"
        };

        var offenders = sourceFiles
            .SelectMany(path => forbiddenDeclarations
                .Where(token => File.ReadAllText(path, Encoding.UTF8).Contains(token, StringComparison.Ordinal))
                .Select(token => $"{Path.GetRelativePath(repositoryRoot, path)} declares local {token}"))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        offenders.Should().BeEmpty("Phase 32.1 requires official Microsoft Agent Framework types, not local stand-ins");
    }

    [Fact]
    public void DynamicToolDescriptors_ShouldBeSqlBackedAndLocalized()
    {
        var repositoryRoot = FindRepositoryRoot();
        var metadataRepository = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "TILSOFTAI.Infrastructure", "SemanticSql", "SqlCapabilityMetadataRepository.cs"),
            Encoding.UTF8);
        var descriptorFactory = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "TILSOFTAI.Orchestration", "AiRouting", "Tools", "CapabilityToolDescriptor.cs"),
            Encoding.UTF8);
        var toolFactory = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "TILSOFTAI.Orchestration", "AiRouting", "Tools", "DynamicFunctionToolFactory.cs"),
            Encoding.UTF8);

        metadataRepository.Should().Contain("ai.CapabilityText");
        metadataRepository.Should().Contain("ai.ArgumentText");
        metadataRepository.Should().Contain("Aliases");
        metadataRepository.Should().Contain("Examples");
        descriptorFactory.Should().Contain("CapabilityToolDescriptor");
        descriptorFactory.Should().Contain("_descriptionBuilder.Build(capability)");
        descriptorFactory.Should().Contain("_schemaBuilder.Build(capability.Arguments");
        toolFactory.Should().Contain("ICapabilityToolDescriptorFactory");
        toolFactory.Should().NotContain("Loaded from SQL", "production model-facing tool descriptions must come from SQL KB metadata, not hardcoded examples");
    }

    [Fact]
    public void OfficialAgentFunctionProvider_ShouldUseOfficialFunctionsAndAvoidDirectSqlCallbacks()
    {
        var repositoryRoot = FindRepositoryRoot();
        var factoryPath = Path.Combine(
            repositoryRoot,
            "src",
            "TILSOFTAI.Orchestration",
            "AiRouting",
            "Tools",
            "DynamicFunctionToolFactory.cs");
        var registrationPath = Path.Combine(
            repositoryRoot,
            "src",
            "TILSOFTAI.Orchestration",
            "OrchestrationServiceCollectionExtensions.cs");
        var factory = File.ReadAllText(factoryPath, Encoding.UTF8);
        var registrations = File.ReadAllText(registrationPath, Encoding.UTF8);

        factory.Should().Contain("IOfficialAgentFunctionProvider");
        factory.Should().Contain(": AIFunction");
        factory.Should().Contain("JsonSchema");
        factory.Should().Contain("_executionFacade.ExecuteReadAsync", "official callbacks should dispatch through CapabilityExecutionFacade for read tools");
        factory.Should().Contain("_executionFacade.PreviewWriteAsync", "official callbacks should dispatch through CapabilityExecutionFacade for preview write tools");
        factory.Should().NotContain("SqlConnection", "official agent tool callbacks must not call SQL directly");
        factory.Should().NotContain("ISqlExecutor", "official agent tool callbacks must not bypass CapabilityExecutionFacade");
        registrations.Should().Contain("IOfficialAgentFunctionProvider, DynamicFunctionToolFactory");
    }

    [Fact]
    public void Architecture_NoLegacyNarrativeSummaryHelpers()
    {
        var repositoryRoot = FindRepositoryRoot();
        var composerPath = Path.Combine(
            repositoryRoot,
            "src",
            "TILSOFTAI.Orchestration",
            "Answering",
            "StructuredAnswerComposer.cs");
        var contents = File.ReadAllText(composerPath, Encoding.UTF8);

        contents.Should().NotContain(string.Concat("BuildDeterministic", "ModelSummary"));
        contents.Should().NotContain(string.Concat("Resolve", "ModelCount"));
        contents.Should().NotContain(string.Concat("ExtractCompared", "ModelCodes"));
        contents.Should().NotContain(string.Concat("TryGet", "ModelCode"));
        contents.Should().NotContain(string.Concat("Is", "ModelCapability"));
    }

    [Fact]
    public void Architecture_NoModelCapabilitySwitchInStructuredAnswerComposer()
    {
        var repositoryRoot = FindRepositoryRoot();
        var composerPath = Path.Combine(
            repositoryRoot,
            "src",
            "TILSOFTAI.Orchestration",
            "Answering",
            "StructuredAnswerComposer.cs");
        var contents = File.ReadAllText(composerPath, Encoding.UTF8);
        var forbiddenCapabilityKeys = new[]
        {
            "model.count",
            "model.overview",
            "model.pieces",
            "model.materials",
            "model.packaging",
            "model.compare"
        };

        contents.Should().NotContainAny(forbiddenCapabilityKeys);
    }

    [Fact]
    public void Architecture_RawJsonDoesNotUseNarration()
    {
        var repositoryRoot = FindRepositoryRoot();
        var rawJsonComposerPath = Path.Combine(
            repositoryRoot,
            "src",
            "TILSOFTAI.Orchestration",
            "Answering",
            "RawJsonAnswerComposer.cs");
        var contents = File.ReadAllText(rawJsonComposerPath, Encoding.UTF8);

        contents.Should().NotContain("IAnswerNarrationService");
        contents.Should().NotContain("AnswerNarrationRequest");
        contents.Should().NotContain("GenerateAsync");
    }

    [Fact]
    public void Architecture_AnswerNarrationIsDomainNeutral()
    {
        var repositoryRoot = FindRepositoryRoot();
        var narrationRoot = Path.Combine(
            repositoryRoot,
            "src",
            "TILSOFTAI.Orchestration",
            "Answering",
            "Narration");
        var offenders = Directory
            .EnumerateFiles(narrationRoot, "*.cs", SearchOption.AllDirectories)
            .SelectMany(path => new[] { "ModelSummary", "model.count", "model.overview", "model.materials", "model.compare" }
                .Where(token => File.ReadAllText(path, Encoding.UTF8).Contains(token, StringComparison.OrdinalIgnoreCase))
                .Select(token => $"{Path.GetRelativePath(repositoryRoot, path)} contains {token}"))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        offenders.Should().BeEmpty("answer narration contracts and services must stay reusable across domains");
    }

    [Fact]
    public void SemanticRetrieval_ShouldUseDomainGateAndCandidateCaps()
    {
        var repositoryRoot = FindRepositoryRoot();
        var semanticRoot = Path.Combine(repositoryRoot, "src", "TILSOFTAI.Orchestration", "Semantic");
        var retriever = File.ReadAllText(Path.Combine(semanticRoot, "SemanticCapabilityRetriever.cs"), Encoding.UTF8);
        var selector = File.ReadAllText(Path.Combine(semanticRoot, "SemanticCapabilityCandidateSelector.cs"), Encoding.UTF8);
        var gate = File.ReadAllText(Path.Combine(semanticRoot, "DomainGate.cs"), Encoding.UTF8);
        var router = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "TILSOFTAI.Orchestration", "AiRouting", "MicrosoftAgentFramework", "OfficialAgentToolRouter.cs"),
            Encoding.UTF8);
        var registrations = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "TILSOFTAI.Orchestration", "OrchestrationServiceCollectionExtensions.cs"),
            Encoding.UTF8);

        retriever.Should().Contain("IDomainGate", "Phase 32.4 requires a first-class domain gate before candidate tool retrieval");
        retriever.Should().Contain("ResolveOptions(options)", "request-level caps should be applied rather than ignored");
        retriever.Should().Contain("domainNames.Length == 0", "an empty domain gate must fail closed instead of running an unrestricted capability search");
        retriever.Should().Contain("Domains = domainNames", "candidate capability retrieval must be scoped to gated domains");
        selector.Should().Contain("ICapabilityCandidateSelector", "Sprint 33 phase 3 requires a first-class candidate selector boundary");
        selector.Should().Contain("SemanticMode", "text fallback mode must be explicit until vector embeddings are active");
        selector.Should().Contain("MaxTotalCandidateTools", "candidate selection must enforce the configured total tool cap");
        router.Should().Contain("ICapabilityCandidateSelector", "the official agent router should receive candidates through the selector boundary");
        gate.Should().Contain("Take(options.MaxDomainsPerRequest)", "domain candidate limits must be enforced in the gate");
        registrations.Should().Contain("IDomainGate, DomainGate");
        registrations.Should().Contain("ICapabilityCandidateSelector, SemanticCapabilityCandidateSelector");
    }

    [Fact]
    public void OfficialAgentResponseMapping_ShouldRouteThroughAnswerComposer()
    {
        var repositoryRoot = FindRepositoryRoot();
        var runtimeRoot = Path.Combine(repositoryRoot, "src", "TILSOFTAI.Orchestration", "AiRouting", "MicrosoftAgentFramework");
        var runtime = File.ReadAllText(Path.Combine(runtimeRoot, "OfficialMicrosoftAgentRuntime.cs"), Encoding.UTF8);
        var responseMapper = File.ReadAllText(Path.Combine(runtimeRoot, "OfficialAgentResponseMapper.cs"), Encoding.UTF8);
        var composerMapper = File.ReadAllText(Path.Combine(runtimeRoot, "AgentToolCallResultMapper.cs"), Encoding.UTF8);

        runtime.Should().Contain("OfficialAgentResponseMapper.ToAgentRunResult");
        responseMapper.Should().Contain("AgentRunOutcome.ToolExecution");
        responseMapper.Should().Contain("AgentRunOutcome.Clarification");
        responseMapper.Should().Contain("ToolResult = invocation.ToolResult");
        composerMapper.Should().Contain("Mode = request.RequestedAnswerMode");
        composerMapper.Should().Contain("agent.no-tool", "no-tool and clarification turns must not be attributed to the first candidate capability");
        composerMapper.Should().NotContain("retrieval.Capabilities.FirstOrDefault()?.Metadata.CapabilityKey");
    }

    [Fact]
    public void WriteTools_ShouldRequirePreviewAndConfirmationFlow()
    {
        var repositoryRoot = FindRepositoryRoot();
        var toolFactory = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "TILSOFTAI.Orchestration", "AiRouting", "Tools", "DynamicFunctionToolFactory.cs"),
            Encoding.UTF8);
        var router = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "TILSOFTAI.Orchestration", "AiRouting", "MicrosoftAgentFramework", "OfficialAgentToolRouter.cs"),
            Encoding.UTF8);
        var facade = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "TILSOFTAI.Orchestration", "Execution", "CapabilityExecutionFacade.cs"),
            Encoding.UTF8);
        var composer = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "TILSOFTAI.Orchestration", "Answering", "StructuredAnswerComposer.cs"),
            Encoding.UTF8);

        toolFactory.Should().Contain("EnableWritePreviewTools", "mutation tools must be explicitly enabled for model exposure");
        toolFactory.Should().Contain("_executionFacade.PreviewWriteAsync");
        toolFactory.Should().NotContain("_executionFacade.ExecuteApprovedWriteAsync", "model-callable tools must never directly execute mutations");
        router.Should().Contain("TryCreateConfirmationTurn");
        router.Should().Contain("_approvalEngine.ApproveAsync");
        router.Should().NotContain(
            "_executionFacade.ExecuteApprovedWriteAsync",
            "Sprint 37 keeps pending action confirmation state but forbids direct write execution from the active router");
        router.Should().Contain("direct write execution is disabled");
        facade.Should().Contain("string approvedActionId");
        facade.Should().Contain("Write operations require an approved action ID.");
        composer.Should().Contain("ConfirmationBlock");
    }

    [Fact]
    public void ActiveOrchestrationDi_ShouldBeModelOnlyAndOfficial()
    {
        var repositoryRoot = FindRepositoryRoot();
        var registrations = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "TILSOFTAI.Orchestration", "OrchestrationServiceCollectionExtensions.cs"),
            Encoding.UTF8);

        registrations.Should().Contain("AddModelOnlyCapabilities");
        registrations.Should().Contain("AddOfficialAgentFrameworkCore");
        registrations.Should().Contain("AddCapabilityExecutionBoundary");
        registrations.Should().Contain("AddAnswerComposer");
        registrations.Should().Contain("AddPendingActionState");
        registrations.Should().NotContain("ModelCapabilities.All");
        registrations.Should().NotContain("new InMemoryCapabilityRegistry");
        registrations.Should().Contain("IOfficialMicrosoftAgentRuntime, OfficialMicrosoftAgentRuntime");
        registrations.Should().NotContain("AddLegacyFallbackOnlyServices");
        registrations.Should().NotContain(string.Concat("Keyword", "IntentClassifier"));
        registrations.Should().NotContain(string.Concat("Structured", "CapabilityResolver"));
        registrations.Should().NotContain(string.Concat("Accounting", "Agent"));
        registrations.Should().NotContain(string.Concat("Warehouse", "Agent"));
    }

    [Fact]
    public void Architecture_NoLegacyDomainAgents()
    {
        AssertNoSourceResidue(
            string.Concat("Accounting", "Agent"),
            string.Concat("Warehouse", "Agent"),
            string.Concat("Domain", "AgentBase"),
            string.Concat("Domain", "AgentRegistry"),
            string.Concat("General", "ChatAgent"),
            string.Concat("Bridge", "FallbackReasons"));
    }

    [Fact]
    public void Architecture_NoKeyword\u0049ntentClassifier()
    {
        AssertNoSourceResidue(
            string.Concat("Keyword", "IntentClassifier"),
            string.Concat("I", "IntentClassifier"),
            string.Concat("Intent", "Classification"));
    }

    [Fact]
    public void Architecture_NoStructured\u0043apabilityResolver()
    {
        AssertNoSourceResidue(
            string.Concat("Structured", "CapabilityResolver"),
            string.Concat("I", "CapabilityResolver"),
            string.Concat("Capability", "RequestHint"));
    }

    [Fact]
    public void Architecture_NoOldTool\u0052egistry()
    {
        AssertNoSourceResidue(
            string.Concat("Tool", "Registry"),
            string.Concat("Tool", "Governance"),
            string.Concat("Tool", "Definition"),
            string.Concat("I", "ToolHandler"),
            string.Concat("Named", "ToolHandlerRegistry"));
    }

    [Fact]
    public void Architecture_NoManualOpenAiToolLoop()
    {
        AssertNoSourceResidue(
            string.Concat("OpenAi", "CompatibleLlmClient"),
            string.Concat("OpenAi", "ResponseParser"),
            string.Concat("I", "LlmClient"));
    }

    [Fact]
    public void Architecture_NoModelCapabilitiesInProduction()
    {
        var repositoryRoot = FindRepositoryRoot();
        var productionOffenders = Directory
            .EnumerateFiles(Path.Combine(repositoryRoot, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(ShouldScan)
            .Where(path => File.ReadAllText(path, Encoding.UTF8).Contains("ModelCapabilities", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(repositoryRoot, path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        productionOffenders.Should().BeEmpty("SQL catalog metadata is now the production source of truth");

        var registrations = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "TILSOFTAI.Api", "Extensions", "AddTilsoftAiSqlExtensions.cs"),
            Encoding.UTF8);
        registrations.Should().Contain("SqlCapabilityCatalogRepository");
        registrations.Should().Contain("ICapabilityRegistry");
        registrations.Should().Contain("ICapabilityMetadataRepository");
    }

    [Fact]
    public void Architecture_OfficialAgentFrameworkOnly()
    {
        var repositoryRoot = FindRepositoryRoot();
        var registrations = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "TILSOFTAI.Orchestration", "OrchestrationServiceCollectionExtensions.cs"),
            Encoding.UTF8);
        var supervisor = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "TILSOFTAI.Orchestration", "Supervisor", "SupervisorRuntime.cs"),
            Encoding.UTF8);

        registrations.Should().Contain("IOfficialMicrosoftAgentRuntime, OfficialMicrosoftAgentRuntime");
        registrations.Should().Contain("IOfficialAgentToolRouter, OfficialAgentToolRouter");
        registrations.Should().Contain("ISupervisorRuntime, SupervisorRuntime");
        supervisor.Should().Contain("_agentToolRouter.TryRouteAsync");
        supervisor.Should().NotContain(string.Concat("Extract", "SubjectKeywords"));
        supervisor.Should().NotContain(string.Concat("Build", "CapabilityHint"));
        supervisor.Should().NotContain(string.Concat("Map", "Request"));
    }

    [Fact]
    public void Sprint32EvalsAndTelemetry_ShouldGuardAgentFrameworkRegressions()
    {
        var repositoryRoot = FindRepositoryRoot();
        var evalRoot = Path.Combine(repositoryRoot, "tests", "TILSOFTAI.Evals");
        var regressionDataset = File.ReadAllText(Path.Combine(evalRoot, "sprint32-agent-framework-regressions.jsonl"), Encoding.UTF8);
        var qualityGates = File.ReadAllText(Path.Combine(evalRoot, "sprint32-quality-gates.json"), Encoding.UTF8);
        var traceModel = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "TILSOFTAI.Orchestration", "Semantic", "SemanticModels.cs"),
            Encoding.UTF8);
        var traceStore = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "TILSOFTAI.Infrastructure", "SemanticSql", "SqlToolRoutingTraceStore.cs"),
            Encoding.UTF8);
        var metrics = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "TILSOFTAI.Domain", "Metrics", "MetricNames.cs"),
            Encoding.UTF8);

        regressionDataset.Should().Contain("first_tool_selection");
        regressionDataset.Should().Contain("regex_only_extraction");
        regressionDataset.Should().Contain("over_exposed_tools");
        regressionDataset.Should().Contain("write_safety");
        qualityGates.Should().Contain("function_selection_accuracy");
        qualityGates.Should().Contain("fake_first_tool_selection");
        qualityGates.Should().Contain("false_write_execution");
        traceModel.Should().Contain("SelectedFunction");
        traceModel.Should().Contain("AdvertisedToolCount");
        traceStore.Should().Contain("@SelectedFunction");
        traceStore.Should().Contain("@AdvertisedToolCount");
        metrics.Should().Contain("AgentRoutingAdvertisedToolCount");
        metrics.Should().Contain("AgentRoutingValidationFailureTotal");
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
        var summaryValidator = Path.Combine(repositoryRoot, "tools", "evidence", "Test-CertificationReviewSummary.ps1");
        var executionSessionGenerator = Path.Combine(repositoryRoot, "tools", "evidence", "New-CertificationExecutionSession.ps1");
        var executionSessionValidator = Path.Combine(repositoryRoot, "tools", "evidence", "Test-CertificationExecutionSession.ps1");
        var executionSummaryGenerator = Path.Combine(repositoryRoot, "tools", "evidence", "New-CertificationExecutionSummary.ps1");
        var acceptanceGenerator = Path.Combine(repositoryRoot, "tools", "evidence", "New-CertificationAcceptance.ps1");
        var acceptanceValidator = Path.Combine(repositoryRoot, "tools", "evidence", "Test-CertificationAcceptance.ps1");
        var acceptanceSummaryGenerator = Path.Combine(repositoryRoot, "tools", "evidence", "New-CertificationAcceptanceSummary.ps1");
        var bundleDocs = Path.Combine(repositoryRoot, "docs", "release_evidence_bundles.md");
        var executionDocs = Path.Combine(repositoryRoot, "docs", "staging_prodlike_certification_execution.md");
        var liveExecutionDocs = Path.Combine(repositoryRoot, "docs", "live_certification_execution_capture.md");
        var signingDecision = Path.Combine(repositoryRoot, "docs", "signed_artifact_verification_decision.md");
        var runTemplate = Path.Combine(repositoryRoot, "docs", "certification_run_manifest.template.json");
        var executionTemplate = Path.Combine(repositoryRoot, "docs", "certification_execution_session.template.json");
        var acceptanceTemplate = Path.Combine(repositoryRoot, "docs", "certification_acceptance.template.json");
        var evidenceRefs = Path.Combine(repositoryRoot, "docs", "certification_evidence_refs.example.json");

        File.Exists(certificationGenerator).Should().BeTrue("Sprint 26 requires a first-class certification run manifest generator");
        File.Exists(certificationValidator).Should().BeTrue("Sprint 26 requires stricter certification evidence validation");
        File.Exists(generator).Should().BeTrue("Sprint 24 requires an executable evidence generation flow");
        File.Exists(validator).Should().BeTrue("Sprint 24 requires generated evidence validation");
        File.Exists(summaryGenerator).Should().BeTrue("Sprint 26 requires a certification review summary generator");
        File.Exists(summaryValidator).Should().BeTrue("Sprint 26 requires a certification review summary gate validator");
        File.Exists(executionSessionGenerator).Should().BeTrue("Sprint 29 requires a certification execution session generator");
        File.Exists(executionSessionValidator).Should().BeTrue("Sprint 29 requires machine-checkable execution session validation");
        File.Exists(executionSummaryGenerator).Should().BeTrue("Sprint 29 requires an execution drill summary generator");
        File.Exists(acceptanceGenerator).Should().BeTrue("Sprint 27 requires a live-certification acceptance generator");
        File.Exists(acceptanceValidator).Should().BeTrue("Sprint 27 requires machine-checkable acceptance validation");
        File.Exists(acceptanceSummaryGenerator).Should().BeTrue("Sprint 27 requires an acceptance go/no-go summary");
        File.Exists(bundleDocs).Should().BeTrue("operators need the bundle convention");
        File.Exists(executionDocs).Should().BeTrue("operators need one staging/prod-like certification execution path");
        File.Exists(liveExecutionDocs).Should().BeTrue("operators need one focused live execution capture path");
        File.Exists(signingDecision).Should().BeTrue("signed artifact verification must be explicitly scoped or deferred");
        File.Exists(runTemplate).Should().BeTrue("certification run manifests should have a stable machine-readable template");
        File.Exists(executionTemplate).Should().BeTrue("certification execution sessions should have a stable machine-readable template");
        File.Exists(acceptanceTemplate).Should().BeTrue("live certification acceptance should have a stable machine-readable template");

        var certificationGeneratorText = File.ReadAllText(certificationGenerator, Encoding.UTF8);
        certificationGeneratorText.Should().Contain("fallbackPosture");
        certificationGeneratorText.Should().Contain("requiredEvidence");
        certificationGeneratorText.Should().Contain("blocked_example");
        certificationGeneratorText.Should().Contain("executionContext");
        certificationGeneratorText.Should().Contain("reviewGate");

        var certificationValidatorText = File.ReadAllText(certificationValidator, Encoding.UTF8);
        certificationValidatorText.Should().Contain("unsupported URI or identifier");
        certificationValidatorText.Should().Contain("Operator signoff evidence is required");
        certificationValidatorText.Should().Contain("Production-like fallback requires fallbackAuthorizationUri");
        certificationValidatorText.Should().Contain("acceptedForReleaseReview");
        certificationValidatorText.Should().Contain("collectedAtUtc");

        var generatorText = File.ReadAllText(generator, Encoding.UTF8);
        generatorText.Should().Contain("CertificationRunPath");
        generatorText.Should().Contain("compatibility_inventory.json");
        generatorText.Should().Contain("fallback-posture.json");
        generatorText.Should().Contain("certification-evidence-manifest.json");
        generatorText.Should().Contain("executionContext");
        generatorText.Should().Contain("fallbackDecision");
        generatorText.Should().Contain("certificationAcceptancePath");
        generatorText.Should().Contain("Get-FileHash");

        var validatorText = File.ReadAllText(validator, Encoding.UTF8);
        validatorText.Should().Contain("Missing certification evidence");
        validatorText.Should().Contain("Production-like fallback was used without authorization evidence");
        validatorText.Should().Contain("RequireAcceptedCertification");

        var summaryGeneratorText = File.ReadAllText(summaryGenerator, Encoding.UTF8);
        summaryGeneratorText.Should().Contain("certification-review-summary.json");
        summaryGeneratorText.Should().Contain("missing_evidence");
        summaryGeneratorText.Should().Contain("fallback_authorization_gap");
        summaryGeneratorText.Should().Contain("review_gate_state_mismatch");

        var summaryValidatorText = File.ReadAllText(summaryValidator, Encoding.UTF8);
        summaryValidatorText.Should().Contain("ready_for_release_review");
        summaryValidatorText.Should().Contain("AllowBlocked");
        summaryValidatorText.Should().Contain("example evidence");

        var executionSessionGeneratorText = File.ReadAllText(executionSessionGenerator, Encoding.UTF8);
        executionSessionGeneratorText.Should().Contain("certification_execution_session.template.json");
        executionSessionGeneratorText.Should().Contain("drillLedger");
        executionSessionGeneratorText.Should().Contain("operator_signoff_incomplete");

        var executionSessionValidatorText = File.ReadAllText(executionSessionValidator, Encoding.UTF8);
        executionSessionValidatorText.Should().Contain("Missing required drill");
        executionSessionValidatorText.Should().Contain("Production-like fallback requires authorized_exception");
        executionSessionValidatorText.Should().Contain("stale");

        var executionSummaryGeneratorText = File.ReadAllText(executionSummaryGenerator, Encoding.UTF8);
        executionSummaryGeneratorText.Should().Contain("certification-execution-summary.json");
        executionSummaryGeneratorText.Should().Contain("Drill Ledger");
        executionSummaryGeneratorText.Should().Contain("ready_for_acceptance");

        var acceptanceGeneratorText = File.ReadAllText(acceptanceGenerator, Encoding.UTF8);
        acceptanceGeneratorText.Should().Contain("certification_acceptance.template.json");
        acceptanceGeneratorText.Should().Contain("ExecutionSessionPath");
        acceptanceGeneratorText.Should().Contain("execution_session_incomplete");
        acceptanceGeneratorText.Should().Contain("example_or_dry_run_evidence");
        acceptanceGeneratorText.Should().Contain("releaseEvidenceBundle");
        acceptanceGeneratorText.Should().Contain("Get-BundleDigest");

        var acceptanceValidatorText = File.ReadAllText(acceptanceValidator, Encoding.UTF8);
        acceptanceValidatorText.Should().Contain("Dry-run/example evidence cannot be accepted as live certification");
        acceptanceValidatorText.Should().Contain("ready_for_release_review");
        acceptanceValidatorText.Should().Contain("Certification acceptance is expired");
        acceptanceValidatorText.Should().Contain("completed certification execution session");
        acceptanceValidatorText.Should().Contain("operator signoff evidence");

        var acceptanceSummaryGeneratorText = File.ReadAllText(acceptanceSummaryGenerator, Encoding.UTF8);
        acceptanceSummaryGeneratorText.Should().Contain("certification-acceptance-summary.json");
        acceptanceSummaryGeneratorText.Should().Contain("goNoGo");
        acceptanceSummaryGeneratorText.Should().Contain("waived_go");

        using var templateDocument = JsonDocument.Parse(File.ReadAllText(runTemplate, Encoding.UTF8));
        var templateRoot = templateDocument.RootElement;
        templateRoot.GetProperty("manifestType").GetString().Should().Be("staging-prodlike-certification-run");
        templateRoot.GetProperty("fallbackPosture").TryGetProperty("fallbackDecision", out _).Should().BeTrue();
        templateRoot.GetProperty("executionContext").TryGetProperty("changeTicketId", out _).Should().BeTrue();
        templateRoot.GetProperty("reviewGate").TryGetProperty("acceptedForReleaseReview", out _).Should().BeTrue();
        ReadEvidenceKinds(templateRoot.GetProperty("requiredEvidence")).Should().BeEquivalentTo(RequiredCertificationEvidenceKinds);

        using var executionTemplateDocument = JsonDocument.Parse(File.ReadAllText(executionTemplate, Encoding.UTF8));
        var executionRoot = executionTemplateDocument.RootElement;
        executionRoot.GetProperty("artifactType").GetString().Should().Be("live-certification-execution-session");
        executionRoot.GetProperty("executionContext").TryGetProperty("executionWindowId", out _).Should().BeTrue();
        executionRoot.GetProperty("decisionInputs").TryGetProperty("requiredDrillsCompleted", out _).Should().BeTrue();
        ReadExecutionDrills(executionRoot.GetProperty("drillLedger")).Should().BeEquivalentTo(RequiredCertificationEvidenceKinds);

        using var acceptanceTemplateDocument = JsonDocument.Parse(File.ReadAllText(acceptanceTemplate, Encoding.UTF8));
        var acceptanceRoot = acceptanceTemplateDocument.RootElement;
        acceptanceRoot.GetProperty("artifactType").GetString().Should().Be("live-certification-acceptance");
        acceptanceRoot.GetProperty("certificationExecutionSession").TryGetProperty("sessionId", out _).Should().BeTrue();
        acceptanceRoot.GetProperty("releaseEvidenceBundle").TryGetProperty("sha256", out _).Should().BeTrue();
        acceptanceRoot.GetProperty("fallbackPosture").TryGetProperty("fallbackDecision", out _).Should().BeTrue();
        acceptanceRoot.GetProperty("decisionInputs").TryGetProperty("exampleEvidencePresent", out _).Should().BeTrue();
        acceptanceRoot.GetProperty("decisionInputs").TryGetProperty("executionSessionComplete", out _).Should().BeTrue();

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

    private static void AssertNoSourceResidue(params string[] forbidden)
    {
        var repositoryRoot = FindRepositoryRoot();
        var offenders = Directory
            .EnumerateFiles(repositoryRoot, "*", SearchOption.AllDirectories)
            .Where(ShouldScanSource)
            .SelectMany(path => forbidden
                .Where(token => File.ReadAllText(path, Encoding.UTF8).Contains(token, StringComparison.Ordinal))
                .Select(token => $"{Path.GetRelativePath(repositoryRoot, path)} contains {token}"))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        offenders.Should().BeEmpty("deleted legacy routing pieces must not return to source or tests");
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

    private static string[] ReadExecutionDrills(JsonElement array)
    {
        return array
            .EnumerateArray()
            .Select(item => item.GetProperty("drillKind").GetString() ?? string.Empty)
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
