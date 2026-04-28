# TILSOFTAIv2 Agent Refactor Plan — English Split Packet

This combined file is for human review. Codex Agent should use the smaller phase files instead.



---

<!-- README.md -->

# TILSOFTAIv2 Agent Refactor Plan — English Phase Packet

## Purpose

This packet is written for Codex Agent or another coding agent that will refactor `bindevz/TILSOFTAIv2` to use **Microsoft Agent Framework** as the intelligent layer for **tool selection** and **parameter extraction**.

The refactor must keep the existing ERP safety foundation: capability catalog, SQL stored procedure whitelist, argument validator, tenant/role policy, approval engine, audit/telemetry, and adapter boundary.

## How to use this packet

Do **not** load every file into a single coding session. Work phase by phase.

Recommended order:

1. `00_ARCHITECTURE_AND_RULES.md`
2. `01_SQL_SERVER_2025_SEMANTIC_KB.md`
3. `02_AGENT_FRAMEWORK_TOOL_ROUTER.md`
4. `03_DYNAMIC_FUNCTION_TOOLS_AND_DESCRIPTIONS.md`
5. `04_CAPABILITY_EXECUTION_FACADE.md`
6. `05_ANSWER_COMPOSER.md`
7. `06_WRITE_PREVIEW_CONFIRMATION.md`
8. `07_COMPOSITE_CAPABILITIES.md`
9. `08_EVAL_OBSERVABILITY_AND_ROLLOUT.md`

Each phase has its own scope, tasks, implementation notes, and acceptance criteria. Finish one phase, run build/tests, then continue.

## Target runtime

```text
API / ChatHub / OpenAI-compatible endpoint
  -> ISupervisorRuntime
  -> MicrosoftAgentToolRouter
      -> hard signal extraction
      -> SQL Server 2025 semantic KB retrieval
      -> domain-gated candidate tools
      -> Microsoft Agent Framework tool calling
  -> CapabilityExecutionFacade
      -> tenant/role policy
      -> entity resolution
      -> argument validation
      -> approval guard
      -> adapter execution
  -> AnswerComposer
      -> raw JSON OR structured answer
```

## Core principle

Microsoft Agent Framework is the **brain for choosing tools and parameters**.

SQL Server 2025 is the **semantic memory/control plane**.

The existing TILSOFTAI governance layer remains the **execution safety boundary**.

## Non-negotiable rules

- Never let the model generate SQL.
- Never expose the full ERP tool catalog to the model.
- Never let a model-facing function call SQL directly.
- Never execute write/create/update/delete operations without user confirmation and approval.
- Never trust model-generated arguments without validation.
- Never leak sensitive fields into AI summarization.
- Do not hardcode production function descriptions in C#.
- Do not reintroduce a technical AI model/provider module if the ERP has a business domain named `model`; use `erp_model`, `product_model`, or `modeling` in code/catalog.

## Phase summary

| Phase | Goal | Output |
|---|---|---|
| 0 | Architecture and feature-flagged integration | Non-breaking entry point in `SupervisorRuntime` |
| 1 | SQL Server 2025 semantic KB | Catalog/description/embedding/entity alias storage |
| 2 | Agent Framework tool router | Candidate-gated agent tool calling |
| 3 | Dynamic function tools | Runtime descriptions and schemas from SQL KB |
| 4 | Capability execution facade | Centralized safe execution boundary |
| 5 | Answer composer | Raw JSON and structured answer modes |
| 6 | Write preview/confirmation | Safe create/update/delete through approval |
| 7 | Composite capabilities | Controlled multi-proc read aggregation |
| 8 | Eval, observability, rollout | Metrics, regression tests, safe deployment |


---

<!-- 00_ARCHITECTURE_AND_RULES.md -->

# Phase 0 — Architecture, Safety Rules, and Feature-Flagged Entry Point

## Goal

Introduce the new Microsoft Agent Framework routing layer without breaking the existing runtime.

This phase should not replace the legacy path yet. It should add a new optional path controlled by feature flags.

## Current runtime to preserve

```text
API / Hub / OpenAI surface
  -> ISupervisorRuntime
  -> IIntentClassifier
  -> CapabilityRequestHint
  -> IAgentRegistry
  -> DomainAgent
  -> CapabilityRegistry
  -> CapabilityResolver
  -> Policy
  -> Validator
  -> ToolAdapter
```

## Target runtime

```text
API / Hub / OpenAI surface
  -> ISupervisorRuntime
  -> IAgentToolRouter.TryRouteAsync(...)
      -> Microsoft Agent Framework tool routing
  -> CapabilityExecutionFacade
  -> IToolAdapter
  -> IAnswerComposer
```

## Keep these existing components

- `ISupervisorRuntime`
- `CapabilityDescriptor`
- `CapabilityArgumentContract`
- `CapabilityArgumentValidator`
- `CapabilityAccessPolicy`
- `IToolAdapterRegistry`
- `SqlToolAdapter`
- `IApprovalEngine`
- `IActionRequestStore`
- `IWriteActionGuard`
- Existing audit/telemetry/readiness infrastructure

## Reduce or retire later

Do not delete these in this phase. They remain fallback components.

- `KeywordIntentClassifier`
- `StructuredCapabilityResolver`
- Manual argument extraction inside domain agents
- Static hardcoded capability descriptions

## Add these abstractions

Create:

```text
src/TILSOFTAI.Orchestration/AiRouting/
```

```csharp
public interface IAgentToolRouter
{
    Task<AgentToolRoutingResult> TryRouteAsync(
        AgentToolRoutingRequest request,
        CancellationToken cancellationToken);
}

public sealed record AgentToolRoutingRequest
{
    public required string Message { get; init; }
    public required TilsoftExecutionContext ExecutionContext { get; init; }
    public required string Locale { get; init; }
    public required AnswerMode RequestedAnswerMode { get; init; }
    public IReadOnlyDictionary<string, object?> Metadata { get; init; } =
        new Dictionary<string, object?>();
}

public sealed record AgentToolRoutingResult
{
    public required bool Handled { get; init; }
    public AssistantAnswer? Answer { get; init; }
    public string? FailureReason { get; init; }
}
```

If `TilsoftExecutionContext`, `AnswerMode`, or `AssistantAnswer` names differ in the current repository, adapt the names while preserving the design.

## Feature flags

Add configuration:

```json
{
  "AiRouting": {
    "MicrosoftAgentFrameworkRoutingEnabled": false,
    "FallbackToLegacyPipeline": true,
    "EnableDynamicToolDescriptions": true,
    "MaxCandidateDomains": 2,
    "MaxCandidateToolsPerDomain": 6,
    "MaxTotalCandidateTools": 12,
    "MaxToolCallsPerTurn": 3,
    "EnableWritePreviewTools": false
  }
}
```

## SupervisorRuntime integration

Add a non-breaking branch before the legacy classifier/resolver path:

```csharp
public async Task<SupervisorResult> RunAsync(
    SupervisorRequest request,
    TilsoftExecutionContext context,
    CancellationToken cancellationToken)
{
    if (_featureFlags.MicrosoftAgentFrameworkRoutingEnabled)
    {
        var routed = await _agentToolRouter.TryRouteAsync(
            new AgentToolRoutingRequest
            {
                Message = request.Input,
                ExecutionContext = context,
                Locale = request.Locale ?? "vi-VN",
                RequestedAnswerMode = ResolveAnswerMode(request)
            },
            cancellationToken);

        if (routed.Handled && routed.Answer is not null)
        {
            return SupervisorResult.FromAssistantAnswer(routed.Answer);
        }

        if (!_featureFlags.FallbackToLegacyPipeline)
        {
            return SupervisorResult.FromFailure(routed.FailureReason ?? "Agent routing failed.");
        }
    }

    return await RunLegacyPipelineAsync(request, context, cancellationToken);
}
```

## Safety rules

The new route must obey these rules from day one:

1. The model must only call provided function tools.
2. Function tools must not call SQL directly.
3. Function tools must call `CapabilityExecutionFacade`.
4. All model-generated arguments must be validated.
5. Write operations must use preview first.
6. Tool count must be capped.
7. The system must be able to fall back to the legacy path.
8. Sensitive fields must not be passed to AI summary without masking.

## Microsoft Agent Framework scope

Use Microsoft Agent Framework for:

```text
natural language -> selected tool -> extracted arguments
```

Do not use it to replace:

```text
policy, validation, approval, SQL execution, audit, answer policy
```

## Phase tasks

- Add routing interfaces.
- Add feature flag options.
- Register an initial no-op/stub `IAgentToolRouter` implementation.
- Wire the router into `SupervisorRuntime` behind the feature flag.
- Add telemetry events for routing attempt, handled, not handled, failure.
- Keep legacy behavior unchanged when the flag is off.

## Acceptance criteria

- The application builds.
- Existing tests pass.
- Feature flag off means old behavior is unchanged.
- Feature flag on can call the stub router and safely fall back.
- No SQL execution behavior changes in this phase.


---

<!-- 01_SQL_SERVER_2025_SEMANTIC_KB.md -->

# Phase 1 — SQL Server 2025 Semantic Knowledge Base

## Goal

Move tool descriptions, multilingual aliases, examples, argument metadata, result schemas, answer policies, and semantic retrieval data into SQL Server 2025.

The production system must not depend on hardcoded C# descriptions for tool selection.

## Design principle

SQL Server 2025 is the semantic control plane.

C# loads metadata from SQL Server and builds candidate tools dynamically.

## Create schema

Create SQL scripts under:

```text
sql/ai/
  001_ai_schema.sql
  002_ai_capability_tables.sql
  003_ai_knowledge_tables.sql
  004_ai_entity_alias_tables.sql
  005_ai_execution_trace_tables.sql
  006_ai_indexes.sql
```

Base schema:

```sql
CREATE SCHEMA ai;
GO
```

## Capability table

```sql
CREATE TABLE ai.Capability
(
    CapabilityID        BIGINT IDENTITY PRIMARY KEY,
    CapabilityKey       NVARCHAR(200) NOT NULL UNIQUE,
    Domain              NVARCHAR(100) NOT NULL,
    BusinessArea        NVARCHAR(100) NULL,
    FunctionName        NVARCHAR(200) NOT NULL,
    AdapterType         NVARCHAR(50) NOT NULL,
    Operation           NVARCHAR(100) NOT NULL,
    StoredProcedure     SYSNAME NULL,
    ExecutionMode       NVARCHAR(50) NOT NULL,
    ArgumentContract    NVARCHAR(MAX) NULL CHECK (ArgumentContract IS NULL OR ISJSON(ArgumentContract) = 1),
    ResultSchema        NVARCHAR(MAX) NULL CHECK (ResultSchema IS NULL OR ISJSON(ResultSchema) = 1),
    AnswerPolicy        NVARCHAR(MAX) NULL CHECK (AnswerPolicy IS NULL OR ISJSON(AnswerPolicy) = 1),
    SensitivityPolicy   NVARCHAR(MAX) NULL CHECK (SensitivityPolicy IS NULL OR ISJSON(SensitivityPolicy) = 1),
    RequiredRoles       NVARCHAR(MAX) NULL CHECK (RequiredRoles IS NULL OR ISJSON(RequiredRoles) = 1),
    AllowedTenants      NVARCHAR(MAX) NULL CHECK (AllowedTenants IS NULL OR ISJSON(AllowedTenants) = 1),
    AllowMultiCall      BIT NOT NULL DEFAULT 0,
    IsActive            BIT NOT NULL DEFAULT 1,
    VersionNo           INT NOT NULL DEFAULT 1,
    UpdatedAt           DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
GO
```

Use native `JSON` columns later if the current SQL Server 2025 deployment, driver, and tooling support them reliably. Start with `NVARCHAR(MAX) + ISJSON` for safer compatibility.

## Capability multilingual text

```sql
CREATE TABLE ai.CapabilityText
(
    CapabilityTextID BIGINT IDENTITY PRIMARY KEY,
    CapabilityKey    NVARCHAR(200) NOT NULL,
    Locale           NVARCHAR(20) NOT NULL,
    ShortName        NVARCHAR(300) NULL,
    Description      NVARCHAR(MAX) NOT NULL,
    UseWhen          NVARCHAR(MAX) NULL,
    DoNotUseWhen     NVARCHAR(MAX) NULL,
    BusinessNotes    NVARCHAR(MAX) NULL,
    UpdatedAt        DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_CapabilityText_Capability
        FOREIGN KEY (CapabilityKey) REFERENCES ai.Capability(CapabilityKey)
);
GO
```

## Capability arguments

```sql
CREATE TABLE ai.CapabilityArgument
(
    ArgumentID          BIGINT IDENTITY PRIMARY KEY,
    CapabilityKey       NVARCHAR(200) NOT NULL,
    ArgumentName        NVARCHAR(128) NOT NULL,
    ProcParameterName   NVARCHAR(128) NOT NULL,
    DataType            NVARCHAR(50) NOT NULL,
    IsRequired          BIT NOT NULL,
    DefaultSource       NVARCHAR(100) NULL,
    ValidationRule      NVARCHAR(MAX) NULL CHECK (ValidationRule IS NULL OR ISJSON(ValidationRule) = 1),
    ClarificationPolicy NVARCHAR(MAX) NULL CHECK (ClarificationPolicy IS NULL OR ISJSON(ClarificationPolicy) = 1),
    DisplayOrder        INT NOT NULL DEFAULT 0,
    IsActive            BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_CapabilityArgument_Capability
        FOREIGN KEY (CapabilityKey) REFERENCES ai.Capability(CapabilityKey)
);
GO
```

## Argument multilingual text

```sql
CREATE TABLE ai.ArgumentText
(
    ArgumentTextID BIGINT IDENTITY PRIMARY KEY,
    CapabilityKey  NVARCHAR(200) NOT NULL,
    ArgumentName   NVARCHAR(128) NOT NULL,
    Locale         NVARCHAR(20) NOT NULL,
    Description    NVARCHAR(MAX) NOT NULL,
    Aliases        NVARCHAR(MAX) NULL CHECK (Aliases IS NULL OR ISJSON(Aliases) = 1),
    Examples       NVARCHAR(MAX) NULL CHECK (Examples IS NULL OR ISJSON(Examples) = 1),
    UpdatedAt      DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
GO
```

## Knowledge chunks for vector retrieval

```sql
CREATE TABLE ai.KnowledgeChunk
(
    ChunkID       BIGINT IDENTITY PRIMARY KEY,
    TenantID      NVARCHAR(100) NULL,
    ChunkType     NVARCHAR(50) NOT NULL,
    ObjectKey     NVARCHAR(300) NOT NULL,
    Domain        NVARCHAR(100) NULL,
    Locale        NVARCHAR(20) NULL,
    Title         NVARCHAR(300) NULL,
    ContentText   NVARCHAR(MAX) NOT NULL,
    Metadata      NVARCHAR(MAX) NULL CHECK (Metadata IS NULL OR ISJSON(Metadata) = 1),
    Embedding     VECTOR(1536) NULL,
    IsActive      BIT NOT NULL DEFAULT 1,
    UpdatedAt     DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
GO
```

`ChunkType` values:

```text
domain | capability | argument | result_field | glossary | example | entity_alias
```

## Entity aliases

```sql
CREATE TABLE ai.EntityAlias
(
    EntityAliasID     BIGINT IDENTITY PRIMARY KEY,
    TenantID          NVARCHAR(100) NULL,
    EntityType        NVARCHAR(100) NOT NULL,
    EntityID          NVARCHAR(100) NOT NULL,
    CanonicalCode     NVARCHAR(100) NULL,
    CanonicalName     NVARCHAR(300) NULL,
    AliasText         NVARCHAR(300) NOT NULL,
    AliasNormalized   NVARCHAR(300) NOT NULL,
    Locale            NVARCHAR(20) NULL,
    Metadata          NVARCHAR(MAX) NULL CHECK (Metadata IS NULL OR ISJSON(Metadata) = 1),
    Embedding         VECTOR(1536) NULL,
    IsActive          BIT NOT NULL DEFAULT 1,
    UpdatedAt         DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
GO
```

Entity types should include:

```text
customer | supplier | factory | warehouse | item | product_model | season | currency | country | employee | po | so | invoice | container
```

## Routing trace

```sql
CREATE TABLE ai.ToolRoutingTrace
(
    TraceID              BIGINT IDENTITY PRIMARY KEY,
    CorrelationID        UNIQUEIDENTIFIER NOT NULL,
    TenantID             NVARCHAR(100) NOT NULL,
    UserID               NVARCHAR(100) NOT NULL,
    Locale               NVARCHAR(20) NULL,
    UserMessageHash      VARBINARY(32) NULL,
    UserMessageRedacted  NVARCHAR(MAX) NULL,
    CandidateDomainsJson NVARCHAR(MAX) NULL CHECK (CandidateDomainsJson IS NULL OR ISJSON(CandidateDomainsJson) = 1),
    CandidateToolsJson   NVARCHAR(MAX) NULL CHECK (CandidateToolsJson IS NULL OR ISJSON(CandidateToolsJson) = 1),
    SelectedTool         NVARCHAR(200) NULL,
    ArgumentsJson        NVARCHAR(MAX) NULL CHECK (ArgumentsJson IS NULL OR ISJSON(ArgumentsJson) = 1),
    ValidationResultJson NVARCHAR(MAX) NULL CHECK (ValidationResultJson IS NULL OR ISJSON(ValidationResultJson) = 1),
    RowCount             INT NULL,
    AnswerMode           NVARCHAR(50) NULL,
    LatencyMs            INT NULL,
    Success              BIT NOT NULL,
    ErrorCode            NVARCHAR(100) NULL,
    CreatedAt            DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
GO
```

## Repositories to add

Create under:

```text
src/TILSOFTAI.Infrastructure/SemanticSql/
```

Suggested interfaces/classes:

```csharp
public interface ISemanticKnowledgeRepository
{
    Task<IReadOnlyList<KnowledgeChunk>> SearchChunksAsync(
        SemanticSearchRequest request,
        CancellationToken cancellationToken);
}

public interface ICapabilityMetadataRepository
{
    Task<CapabilitySemanticMetadata> GetCapabilityMetadataAsync(
        string capabilityKey,
        string locale,
        CancellationToken cancellationToken);
}

public interface IEntityAliasRepository
{
    Task<IReadOnlyList<EntityCandidate>> SearchAliasesAsync(
        EntityAliasSearchRequest request,
        CancellationToken cancellationToken);
}

public interface IToolRoutingTraceStore
{
    Task SaveAsync(ToolRoutingTrace trace, CancellationToken cancellationToken);
}
```

## Seed data

Seed only 5 to 10 priority capabilities first:

```text
warehouse.inventory.by-item
warehouse.stock-movement.by-item
purchasing.po.open-by-supplier
sales.orders.by-customer
accounting.receivables.by-customer
product_model.by-code
```

For each seed capability, insert:

- `ai.Capability`
- `ai.CapabilityText` for `vi-VN` and `en-US`
- `ai.CapabilityArgument`
- `ai.ArgumentText` for `vi-VN` and `en-US`
- `ai.KnowledgeChunk` rows for capability, examples, arguments, and glossary terms
- relevant `ai.EntityAlias` examples

## Retrieval scoring

Implement a combined score:

```text
score =
  0.45 * vector_similarity
+ 0.20 * exact_alias_match
+ 0.15 * hard_signal_match
+ 0.10 * domain_prior
+ 0.10 * recent_success_trace_score
```

The exact formula can be adjusted, but retrieval must combine semantic, keyword, alias, and hard signal evidence.

## Acceptance criteria

- SQL scripts create the `ai` schema and core tables.
- Seed data exists for at least five priority capabilities.
- Repositories can load localized capability/argument metadata.
- Repositories can retrieve candidate domains/capabilities from user text.
- No production function description is hardcoded in C#.
- Vector support is optional during the first implementation but the schema must support it.


---

<!-- 02_AGENT_FRAMEWORK_TOOL_ROUTER.md -->

# Phase 2 — Microsoft Agent Framework Tool Router

## Goal

Implement an `IAgentToolRouter` using Microsoft Agent Framework as the intelligent layer for selecting ERP function tools and extracting parameters from natural language.

The router must expose only a small candidate tool set per request.

## Runtime flow

```text
User message
  -> hard signal extraction
  -> SQL Server 2025 semantic KB retrieval
  -> candidate domains
  -> candidate capabilities
  -> dynamic function tools
  -> Microsoft Agent Framework agent
  -> tool call result
  -> AnswerComposer
```

## Package direction

Add the Microsoft Agent Framework package to the orchestration project:

```bash
dotnet add src/TILSOFTAI.Orchestration package Microsoft.Agents.AI
```

Add the provider package required by the deployment environment, but hide provider-specific code behind an internal factory.

## Add router implementation

Create:

```text
src/TILSOFTAI.Orchestration/AiRouting/MicrosoftAgentFramework/
  MicrosoftAgentToolRouter.cs
  AgentInstructionsBuilder.cs
  AgentRunOptionsFactory.cs
  AgentClientFactory.cs
  AgentToolCallResultMapper.cs
```

Skeleton:

```csharp
public sealed class MicrosoftAgentToolRouter : IAgentToolRouter
{
    private readonly IHardSignalExtractor _hardSignalExtractor;
    private readonly ISemanticCapabilityRetriever _retriever;
    private readonly IAgentFunctionToolFactory _toolFactory;
    private readonly IAgentClientFactory _agentClientFactory;
    private readonly IAnswerComposer _answerComposer;
    private readonly IToolRoutingTraceStore _traceStore;

    public async Task<AgentToolRoutingResult> TryRouteAsync(
        AgentToolRoutingRequest request,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var hardSignals = _hardSignalExtractor.Extract(
                request.Message,
                request.Locale,
                request.ExecutionContext);

            var retrieval = await _retriever.RetrieveAsync(
                request.Message,
                hardSignals,
                request.ExecutionContext,
                request.Locale,
                CapabilityRetrievalOptions.Default,
                cancellationToken);

            if (retrieval.Capabilities.Count == 0)
            {
                return new AgentToolRoutingResult
                {
                    Handled = false,
                    FailureReason = "No candidate capabilities found."
                };
            }

            var tools = await _toolFactory.BuildToolsAsync(
                retrieval.Capabilities,
                request.ExecutionContext,
                request.Locale,
                cancellationToken);

            if (tools.Count == 0)
            {
                return new AgentToolRoutingResult
                {
                    Handled = false,
                    FailureReason = "No tools created from candidate capabilities."
                };
            }

            var agent = _agentClientFactory.CreateToolCallingAgent(
                tools,
                AgentInstructionsBuilder.Build(request, hardSignals, retrieval));

            var agentResult = await agent.RunAsync(
                request.Message,
                cancellationToken);

            var answerRequest = AgentToolCallResultMapper.ToAnswerComposerRequest(
                agentResult,
                request);

            var answer = await _answerComposer.ComposeAsync(
                answerRequest,
                cancellationToken);

            await _traceStore.SaveAsync(
                ToolRoutingTrace.FromSuccess(request, retrieval, agentResult, answer, startedAt),
                cancellationToken);

            return new AgentToolRoutingResult
            {
                Handled = true,
                Answer = answer
            };
        }
        catch (Exception ex)
        {
            await _traceStore.SaveAsync(
                ToolRoutingTrace.FromFailure(request, ex, startedAt),
                cancellationToken);

            return new AgentToolRoutingResult
            {
                Handled = false,
                FailureReason = ex.Message
            };
        }
    }
}
```

Adapt method names to the exact Microsoft Agent Framework APIs used in the project.

## Hard signal extraction

Create:

```text
src/TILSOFTAI.Orchestration/Semantic/
```

```csharp
public interface IHardSignalExtractor
{
    HardSignalSet Extract(
        string message,
        string locale,
        TilsoftExecutionContext context);
}

public sealed record HardSignalSet
{
    public IReadOnlyList<CodeSignal> Codes { get; init; } = [];
    public IReadOnlyList<DateSignal> Dates { get; init; } = [];
    public IReadOnlyList<NumberSignal> Numbers { get; init; } = [];
    public IReadOnlyList<string> BusinessKeywords { get; init; } = [];
    public string? DetectedLanguage { get; init; }
}
```

Use hard signals for retrieval improvement only. Do not treat them as final authority.

Examples:

```text
"PO-123" -> possible purchase order number
"SO-9001" -> possible sales order number
"MADEIRA-BLK" -> possible item/product code
"BD" -> possible warehouse code
"last month" -> relative date range
"tháng trước" -> relative date range
"500 pcs" -> quantity signal
```

## Semantic capability retriever

Create:

```csharp
public interface ISemanticCapabilityRetriever
{
    Task<CapabilityRetrievalResult> RetrieveAsync(
        string userMessage,
        HardSignalSet hardSignals,
        TilsoftExecutionContext context,
        string locale,
        CapabilityRetrievalOptions options,
        CancellationToken cancellationToken);
}

public sealed record CapabilityRetrievalResult
{
    public required IReadOnlyList<DomainCandidate> Domains { get; init; }
    public required IReadOnlyList<CapabilityCandidate> Capabilities { get; init; }
    public required IReadOnlyList<EntityCandidate> EntityCandidates { get; init; }
    public required IReadOnlyList<KnowledgeChunk> ContextChunks { get; init; }
}
```

Routing levels:

```text
Level 0: hard signal extraction
Level 1: retrieve top 1-3 domains
Level 2: retrieve top 5-12 capabilities across selected domains
Level 3: build function tools only for those capabilities
Level 4: let the agent choose a tool and arguments
```

## Tool budget policy

Default policy:

```json
{
  "maxDomainsPerRequest": 2,
  "maxToolsPerDomain": 6,
  "maxTotalTools": 12,
  "maxToolCallsPerTurn": 3,
  "allowParallelReadTools": true,
  "allowParallelWriteTools": false,
  "allowModelSelectedMultiTool": false,
  "preferCompositeCapability": true
}
```

Implementation requirement:

- Never expose the full ERP tool catalog.
- Never expose write execution tools.
- Cap candidate tools before sending them to the model.
- Prefer one primary tool per request in the first release.

## Agent instructions

Build instructions dynamically but keep the safety rules stable:

```text
You are an internal ERP assistant.

Rules:
- Use only the provided ERP function tools.
- Never generate SQL.
- Never invent item codes, customer codes, supplier codes, warehouse IDs, invoice numbers, PO numbers, or SO numbers.
- If required parameters are missing, ask a concise clarification question.
- If multiple entity candidates are possible, ask the user to choose.
- For create/update/delete/post/approve/cancel actions, only use preview tools unless an approved action is explicitly provided by the system.
- Do not mention stored procedure names unless debug mode is enabled.
- Reply in the user's locale unless system configuration says otherwise.
```

Append request-local context:

```text
Locale: {locale}
Tenant: {tenant_id}
User roles: {role summary, no secrets}
Candidate domains: {top domains}
Candidate tool count: {n}
Current business date/time: {business timezone}
```

## Acceptance criteria

- User can ask a Vietnamese or English ERP question.
- Router retrieves a small set of candidate tools.
- Agent receives only candidate tools, not the full catalog.
- Agent can select the correct priority capability.
- Agent can extract basic parameters for priority capabilities.
- Router falls back safely if retrieval or agent execution fails.
- All tool calls go through the tool factory and then `CapabilityExecutionFacade`.


---

<!-- 03_DYNAMIC_FUNCTION_TOOLS_AND_DESCRIPTIONS.md -->

# Phase 3 — Dynamic Function Tools and Multilingual Descriptions

## Goal

Build model-facing function tools dynamically from SQL Server 2025 semantic metadata instead of hardcoding descriptions and aliases in C#.

The system is multilingual. Tool and parameter descriptions must be localized and generated at runtime.

## Key rule

Do not create a generic production tool like:

```text
execute_sql(proc_name, json)
execute_capability(capability_key, arguments_json)
```

Instead, create semantic, specific model-facing functions:

```text
warehouse_inventory_by_item(item_no, warehouse_code)
purchasing_open_purchase_orders_by_supplier(supplier_code, status, from_date, to_date)
sales_orders_by_customer(customer_code, from_date, to_date)
accounting_receivables_by_customer(customer_code, as_of_date)
```

Internally, all functions may call a generic executor:

```csharp
await _capabilityExecutionFacade.ExecuteReadAsync(capabilityKey, arguments, cancellationToken);
```

## Add tool factory

Create:

```text
src/TILSOFTAI.Orchestration/AiRouting/Tools/
  IAgentFunctionToolFactory.cs
  DynamicFunctionToolFactory.cs
  CapabilityFunctionNameMapper.cs
  CapabilityToolDescriptionBuilder.cs
  CapabilityParameterSchemaBuilder.cs
```

Interface:

```csharp
public interface IAgentFunctionToolFactory
{
    Task<IReadOnlyList<object>> BuildToolsAsync(
        IReadOnlyList<CapabilityCandidate> candidates,
        TilsoftExecutionContext context,
        string locale,
        CancellationToken cancellationToken);
}
```

Use the exact Microsoft Agent Framework function-tool type in the implementation. Keep the public interface adaptable if provider-specific types are difficult to abstract.

## Metadata sources

For each candidate capability, load:

- `ai.Capability.FunctionName`
- `ai.Capability.ExecutionMode`
- `ai.CapabilityText` for requested locale
- fallback `ai.CapabilityText` for default locale
- `ai.CapabilityArgument`
- `ai.ArgumentText` for requested locale
- argument aliases and examples
- result schema summary
- answer policy summary
- sensitivity policy summary
- top related examples from `ai.KnowledgeChunk`

## Description template

Generate a function description like this:

```text
Business domain: {domain}
Purpose: {localized_description}
Use when: {use_when}
Do not use when: {do_not_use_when}
Input requirements:
- {argument_name}: {argument_description}. Aliases: {top_aliases}. Examples: {top_examples}.
Execution mode: {execution_mode}
Safety: Never invent IDs. Ask clarification if required inputs are missing or ambiguous.
```

Keep descriptions concise. The agent receives only candidate tools, but each description still affects tool selection quality.

## Parameter schema strategy

Map `ai.CapabilityArgument` to model-facing parameter schema:

```text
ArgumentName        -> model-facing name, e.g. item_no
ProcParameterName   -> SQL-facing name, e.g. @ItemNo
DataType            -> string, integer, decimal, boolean, date, datetime, enum, object, array
IsRequired          -> required schema property
ValidationRule      -> allowed values, regex, min/max, format
ClarificationPolicy -> question to ask when missing/invalid
```

Parameter descriptions must come from `ai.ArgumentText`.

## Function callback behavior

Each generated tool callback must:

1. Receive model-facing arguments.
2. Attach metadata: capability key, function name, locale, correlation ID.
3. Call `CapabilityExecutionFacade`.
4. Return a `CapabilityExecutionEnvelope` or equivalent safe result.

Pseudo-code:

```csharp
private async Task<CapabilityExecutionEnvelope> InvokeCapabilityAsync(
    string capabilityKey,
    IReadOnlyDictionary<string, object?> modelFacingArguments,
    TilsoftExecutionContext context,
    CancellationToken cancellationToken)
{
    var capability = await _capabilityRepository.GetAsync(capabilityKey, cancellationToken);

    return capability.ExecutionMode switch
    {
        "read_only" => await _facade.ExecuteReadAsync(
            capabilityKey,
            modelFacingArguments,
            cancellationToken),

        "write_preview" => await _facade.PreviewWriteAsync(
            capabilityKey,
            modelFacingArguments,
            cancellationToken),

        "composite" => await _compositeExecutor.ExecuteAsync(
            capabilityKey,
            modelFacingArguments,
            cancellationToken),

        _ => CapabilityExecutionEnvelope.Blocked(
            capabilityKey,
            $"Unsupported execution mode: {capability.ExecutionMode}")
    };
}
```

## Naming conventions

Use clear function names:

```text
{domain}_{business_action}_{business_object}_{qualifier}
```

Examples:

```text
warehouse_inventory_by_item
warehouse_stock_movement_by_item
purchasing_open_purchase_orders_by_supplier
sales_orders_by_customer
accounting_receivables_by_customer
product_model_by_code
```

Avoid names like:

```text
run_proc
query_data
get_data
execute_tool
model_query
```

## Description hardcoding rule

Allowed in POC only:

```csharp
[Description("Temporary POC description")]
```

Not allowed in production:

```csharp
[Description("Permanent ERP tool description")]
```

Production descriptions must come from SQL KB.

## Domain gating

The factory must receive candidates from the retriever. It must not query and build all active capabilities.

Hard limits:

```text
maxDomainsPerRequest <= 2
maxToolsPerDomain <= 6
maxTotalTools <= 12
```

## Acceptance criteria

- Function names are generated from capability metadata.
- Function descriptions are loaded from SQL KB.
- Parameter descriptions are loaded from SQL KB.
- The model sees specific semantic functions, not generic executor functions.
- The tool factory builds only candidate tools.
- Tool callbacks never call SQL directly.
- Tool callbacks invoke `CapabilityExecutionFacade`.


---

<!-- 04_CAPABILITY_EXECUTION_FACADE.md -->

# Phase 4 — CapabilityExecutionFacade

## Goal

Create a single safe execution boundary between model-facing tools and ERP adapters.

All function tools must call `CapabilityExecutionFacade`. No function tool may call `SqlToolAdapter`, `SqlExecutor`, stored procedures, views, or ERP APIs directly.

## Add facade

Create:

```text
src/TILSOFTAI.Orchestration/Execution/
  ICapabilityExecutionFacade.cs
  CapabilityExecutionFacade.cs
  CapabilityExecutionEnvelope.cs
  CapabilityArgumentMapper.cs
  CapabilityExecutionPolicy.cs
```

Interface:

```csharp
public interface ICapabilityExecutionFacade
{
    Task<CapabilityExecutionEnvelope> ExecuteReadAsync(
        string capabilityKey,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken);

    Task<CapabilityExecutionEnvelope> PreviewWriteAsync(
        string capabilityKey,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken);

    Task<CapabilityExecutionEnvelope> ExecuteApprovedWriteAsync(
        string capabilityKey,
        string approvedActionId,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken);
}
```

## Envelope

```csharp
public sealed record CapabilityExecutionEnvelope
{
    public required string CapabilityKey { get; init; }
    public required string ExecutionMode { get; init; }
    public string? ProcedureName { get; init; }
    public required IReadOnlyDictionary<string, object?> Arguments { get; init; }
    public ResultSchema? ResultSchema { get; init; }
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; init; } = [];
    public int RowCount { get; init; }
    public required ExecutionMetadata ExecutionMetadata { get; init; }
    public required SensitivityPolicy SensitivityPolicy { get; init; }
    public required AnswerPolicy AnswerPolicy { get; init; }
    public string? ClarificationQuestion { get; init; }
    public IReadOnlyList<string> MissingArguments { get; init; } = [];
    public IReadOnlyList<string> InvalidArguments { get; init; } = [];
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public bool Success { get; init; }
}
```

Adapt types to match existing project models.

## Responsibilities

The facade must perform these steps:

```text
1. Load CapabilityDescriptor by capabilityKey.
2. Check active/version/certification state.
3. Evaluate tenant/user/role access.
4. Resolve entity aliases.
5. Normalize locale-specific values.
6. Map model-facing argument names to proc parameter names.
7. Validate argument contract using existing validator.
8. Enforce execution mode.
9. Enforce write preview/approval rules.
10. Call IToolAdapterRegistry and execute selected adapter.
11. Build CapabilityExecutionEnvelope.
12. Emit audit/telemetry.
```

## Read execution flow

```csharp
public async Task<CapabilityExecutionEnvelope> ExecuteReadAsync(
    string capabilityKey,
    IReadOnlyDictionary<string, object?> arguments,
    CancellationToken cancellationToken)
{
    var capability = await _capabilityRegistry.GetRequiredAsync(capabilityKey, cancellationToken);
    var context = _executionContextAccessor.GetRequired();

    var access = _accessPolicy.Evaluate(capability, context);
    if (!access.Allowed)
    {
        return CapabilityExecutionEnvelopeFactory.AccessDenied(capability, access);
    }

    if (!string.Equals(capability.ExecutionMode, "read_only", StringComparison.OrdinalIgnoreCase))
    {
        return CapabilityExecutionEnvelopeFactory.Blocked(
            capability,
            "Capability is not read-only. Use preview/approval flow.");
    }

    var resolvedArgs = await _entityAliasResolver.ResolveAsync(
        capability,
        arguments,
        context,
        cancellationToken);

    var procArgs = _argumentMapper.MapModelArgsToProcArgs(capability, resolvedArgs);

    var validation = _argumentValidator.Validate(capability, procArgs);
    if (!validation.IsValid)
    {
        return CapabilityExecutionEnvelopeFactory.ValidationFailed(
            capability,
            procArgs,
            validation);
    }

    var adapter = _toolAdapterRegistry.Resolve(capability.AdapterType);

    var request = ToolExecutionRequestFactory.Create(
        capability,
        procArgs,
        context);

    var result = await adapter.ExecuteAsync(request, cancellationToken);

    return CapabilityExecutionEnvelopeFactory.FromToolResult(
        capability,
        procArgs,
        result,
        context);
}
```

## Argument mapping

Model-facing arguments:

```json
{
  "item_no": "MADEIRA-BLK",
  "warehouse_code": "BD"
}
```

SQL-facing arguments:

```json
{
  "@ItemNo": "MADEIRA-BLK",
  "@WarehouseCode": "BD"
}
```

Mapping source:

```text
ai.CapabilityArgument.ArgumentName
ai.CapabilityArgument.ProcParameterName
```

## Entity alias resolution

Resolve only when the capability contract declares the argument as an entity or when the argument text indicates an entity type.

Examples:

```text
"BD" -> warehouse code / warehouse id
"ABC" -> customer or supplier candidate
"MADEIRA-BLK" -> item/product candidate
```

If multiple candidates are plausible, do not guess. Return a clarification envelope.

## Validation failures

Validation failure must not call SQL.

Return an envelope with:

```text
Success = false
MissingArguments
InvalidArguments
ClarificationQuestion
ErrorCode = "ARGUMENT_VALIDATION_FAILED"
```

## Adapter execution

Preferred SQL exposure:

```text
AI capability -> stored procedure wrapper -> ERP view/table/domain logic
```

Do not let the AI call views directly. Use wrapper stored procedures for:

- tenant filtering
- row limits
- stable result schema
- auditability
- parameter normalization
- security checks

## Acceptance criteria

- No function tool calls SQL or adapters directly.
- All execution goes through `CapabilityExecutionFacade`.
- Missing required arguments return clarification, not SQL errors.
- Unauthorized capabilities are blocked.
- Arguments are mapped from model-facing names to proc parameter names.
- Existing `CapabilityArgumentValidator` is used.
- Existing adapter registry is used.
- Read-only capabilities execute successfully through the facade.


---

<!-- 05_ANSWER_COMPOSER.md -->

# Phase 5 — Answer Composer

## Goal

Create a clear response composition layer with two main modes:

1. `RawJson`: return deterministic raw JSON without AI summarization.
2. `Structured`: receive capability/proc/arguments/result schema/rows/metadata/policy/locale and decide whether to return text, table, chart, summary, confirmation, or follow-up question.

## Add answer composer folder

Create:

```text
src/TILSOFTAI.Orchestration/Answering/
  IAnswerComposer.cs
  AnswerComposerRequest.cs
  AssistantAnswer.cs
  AnswerBlock.cs
  RawJsonAnswerComposer.cs
  StructuredAnswerComposer.cs
  AiSummaryService.cs
  ResultSchema.cs
  AnswerPolicy.cs
  SensitivityPolicy.cs
```

## Interface

```csharp
public interface IAnswerComposer
{
    Task<AssistantAnswer> ComposeAsync(
        AnswerComposerRequest request,
        CancellationToken cancellationToken);
}

public enum AnswerMode
{
    RawJson,
    Structured
}

public sealed record AnswerComposerRequest
{
    public required AnswerMode Mode { get; init; }
    public required string CapabilityKey { get; init; }
    public required string? ProcedureName { get; init; }
    public required IReadOnlyDictionary<string, object?> Arguments { get; init; }
    public required ResultSchema? ResultSchema { get; init; }
    public required IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; init; }
    public required int RowCount { get; init; }
    public required ExecutionMetadata ExecutionMetadata { get; init; }
    public required SensitivityPolicy SensitivityPolicy { get; init; }
    public required string Locale { get; init; }
    public required AnswerPolicy AnswerPolicy { get; init; }
    public string? ClarificationQuestion { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}
```

## Output model

```csharp
public sealed record AssistantAnswer
{
    public required string AnswerType { get; init; }
    public required string Text { get; init; }
    public required IReadOnlyList<AnswerBlock> Blocks { get; init; }
    public IReadOnlyList<string> FollowUpQuestions { get; init; } = [];
    public required AnswerProvenance Provenance { get; init; }
}

public abstract record AnswerBlock(string Type);

public sealed record TextBlock(string Content) : AnswerBlock("text");

public sealed record TableBlock(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<object?>> Rows,
    int TotalRows,
    bool Truncated) : AnswerBlock("table");

public sealed record ChartBlock(
    string ChartType,
    string CategoryField,
    string ValueField,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Data) : AnswerBlock("chart");

public sealed record FollowUpBlock(
    string Question,
    IReadOnlyList<string> Options) : AnswerBlock("follow_up");

public sealed record ConfirmationBlock(
    string Title,
    string Summary,
    IReadOnlyDictionary<string, object?> DraftAction) : AnswerBlock("confirmation");

public sealed record RawJsonBlock(object Data) : AnswerBlock("raw_json");
```

## Raw JSON mode

Rules:

- Do not call the LLM.
- Apply sensitivity policy before returning data.
- Return deterministic envelope containing:
  - capability key
  - arguments
  - row count
  - rows
  - result schema
  - execution metadata
  - provenance

Example:

```json
{
  "answerType": "raw_json",
  "text": "",
  "blocks": [
    {
      "type": "raw_json",
      "data": {
        "capabilityKey": "warehouse.inventory.by-item",
        "arguments": {
          "@ItemNo": "MADEIRA-BLK"
        },
        "rowCount": 2,
        "rows": []
      }
    }
  ],
  "provenance": {
    "capabilityKey": "warehouse.inventory.by-item",
    "rowCount": 2,
    "correlationId": "..."
  }
}
```

## Structured mode behavior

Decision rules:

```text
If clarification is required:
  return follow-up question.

If validation failed:
  return concise error + missing/invalid arguments.

If rowCount = 0:
  explain no data found and repeat filters used.

If rowCount <= AnswerPolicy.MaxRowsForChat:
  return summary + table.

If rowCount > AnswerPolicy.MaxRowsForChat:
  return summary + top rows + truncated flag + suggest filter/export.

If result schema has date/time + numeric measure:
  add line chart candidate.

If result schema has category + numeric measure:
  add bar chart candidate.

If execution mode is write_preview:
  return confirmation block.

If sensitivity policy masks columns:
  mask before display and before AI summary.
```

## Result schema

Result schema should include labels and semantic roles:

```json
{
  "columns": [
    {
      "name": "WarehouseName",
      "label": "Warehouse",
      "type": "string",
      "role": "dimension",
      "visible": true
    },
    {
      "name": "AvailableQty",
      "label": "Available Quantity",
      "type": "decimal",
      "role": "measure",
      "format": "number",
      "visible": true
    }
  ],
  "defaultSort": [
    {
      "column": "AvailableQty",
      "direction": "desc"
    }
  ],
  "chartHints": [
    {
      "type": "bar",
      "category": "WarehouseName",
      "value": "AvailableQty"
    }
  ]
}
```

## AI summary policy

AI summary is optional and only allowed after deterministic masking/filtering.

Allowed input to model:

- safe rows only
- capped row set
- result schema labels
- arguments used
- locale
- business meaning from result schema

Never pass:

- secrets
- hidden columns
- unmasked PII/financial fields if policy forbids
- raw stored procedure names unless debug mode is enabled
- full large result sets beyond policy cap

## Locale behavior

Format according to locale:

- dates
- numbers
- currency
- decimal separators
- table labels
- summary language
- clarification questions

Default locale should come from request/user/tenant, with fallback to `vi-VN` if current product defaults to Vietnamese.

## Acceptance criteria

- `RawJson` mode returns deterministic JSON and does not call LLM.
- `Structured` mode returns `TextBlock`, `TableBlock`, `ChartBlock`, `FollowUpBlock`, or `ConfirmationBlock` as appropriate.
- Row count zero is handled clearly.
- Large result sets are truncated according to policy.
- Sensitive fields are masked before display and before AI summary.
- Provenance includes capability key, row count, and correlation ID.


---

<!-- 06_WRITE_PREVIEW_CONFIRMATION.md -->

# Phase 6 — Write Preview, User Confirmation, and Approval

## Goal

Allow users to create/update/delete ERP data through chat, but only through a safe two-step flow:

```text
preview -> user confirmation -> approval -> execute
```

The agent must never execute write operations directly from a single natural-language request.

## Model-facing tools

Expose preview tools only:

```text
sales_order_create_preview
purchasing_po_create_preview
warehouse_receipt_create_preview
product_model_create_preview
```

Do not expose execution tools:

```text
sales_order_create_execute
purchasing_po_create_execute
warehouse_receipt_create_execute
```

## Flow

```text
User asks to create/update/delete data
  -> agent calls *_preview function
  -> CapabilityExecutionFacade.PreviewWriteAsync
  -> validate user permissions and business rules
  -> AnswerComposer returns ConfirmationBlock
  -> user confirms
  -> existing IApprovalEngine creates/approves action
  -> ExecuteApprovedWriteAsync with approvedActionId
  -> SqlToolAdapter executes write proc
  -> AnswerComposer returns success/failure
```

## Preview behavior

`PreviewWriteAsync` must:

- Load the capability.
- Verify it is a write preview capability.
- Check tenant/user/role access.
- Resolve entities.
- Validate arguments.
- Run business-rule validation.
- Return a draft action payload.
- Not execute the write stored procedure.

Preview result should be returned as a confirmation block:

```csharp
public sealed record ConfirmationBlock(
    string Title,
    string Summary,
    IReadOnlyDictionary<string, object?> DraftAction) : AnswerBlock("confirmation");
```

## User confirmation

The system must treat user confirmation as an explicit action.

Examples of valid confirmation intent:

```text
Confirm
Yes, create it
Xác nhận
Tạo đi
Đồng ý
```

Do not execute if confirmation is ambiguous.

## Approval engine

Reuse existing approval infrastructure:

- `IApprovalEngine`
- `IActionRequestStore`
- `IWriteActionGuard`
- existing `SqlToolAdapter` requirement for `approvedActionId`

`SqlToolAdapter` must still reject write execution if `approvedActionId` is missing.

## Execution behavior

`ExecuteApprovedWriteAsync` must:

1. Load the approved draft/action.
2. Verify the action belongs to the current tenant/user/session or authorized approver.
3. Verify the approval has not expired.
4. Verify the same capability and arguments are being executed.
5. Pass `approvedActionId` to the adapter.
6. Audit the final result.

## Agent Framework tool approval

If the selected Microsoft Agent Framework provider supports tool approval or human-in-the-loop hooks, it may be used as an additional UX/runtime feature.

However, the existing ERP `IApprovalEngine` remains the source of truth for write authorization.

## Capability catalog requirements

Write capabilities should be represented separately:

```json
{
  "capabilityKey": "purchasing.po.create-preview",
  "domain": "purchasing",
  "functionName": "purchasing_po_create_preview",
  "executionMode": "write_preview"
}
```

Actual execution capability:

```json
{
  "capabilityKey": "purchasing.po.create-execute",
  "domain": "purchasing",
  "functionName": "purchasing_po_create_execute",
  "executionMode": "write_execute"
}
```

Only `write_preview` function tools are exposed to the model.

## Acceptance criteria

- A user cannot create/update/delete ERP data in one step.
- The agent can prepare a write preview.
- The answer composer returns a confirmation block.
- Actual write execution requires explicit user confirmation.
- Actual write execution requires `approvedActionId`.
- `SqlToolAdapter` still blocks writes without approval.
- All write preview and execution events are audited.


---

<!-- 07_COMPOSITE_CAPABILITIES.md -->

# Phase 7 — Composite Capabilities for Multi-Proc Read Aggregation

## Goal

Support user questions that require multiple data sources or stored procedures, while avoiding uncontrolled model-driven tool chaining.

Prefer declarative composite capabilities over free-form multi-tool calls.

## Principle

The model should select a composite capability when the business intent is known.

The system, not the model, decides which sub-capabilities are executed.

## Example

User asks:

```text
Give me a 360 view of customer ABC.
```

Composite capability:

```json
{
  "capabilityKey": "sales.customer.360",
  "domain": "sales",
  "functionName": "sales_customer_360",
  "executionMode": "composite",
  "subCapabilities": [
    "sales.orders.by-customer",
    "accounting.receivables.by-customer",
    "warehouse.pending-shipments.by-customer"
  ],
  "compositionPolicy": {
    "mode": "parallel",
    "joinKey": "CustomerCode",
    "output": "json_bundle"
  }
}
```

## Add executor

Create:

```text
src/TILSOFTAI.Orchestration/Execution/
  CompositeCapabilityExecutor.cs
```

Interface:

```csharp
public interface ICompositeCapabilityExecutor
{
    Task<CapabilityExecutionEnvelope> ExecuteAsync(
        string compositeCapabilityKey,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken);
}
```

## Execution flow

```text
CompositeCapabilityExecutor
  -> load composite capability
  -> validate shared arguments
  -> load sub-capabilities
  -> verify all are read-only unless explicitly allowed
  -> execute sub-capabilities through CapabilityExecutionFacade
  -> merge result envelopes
  -> return merged envelope to AnswerComposer
```

## Composition policies

Support at least:

```text
parallel
sequential
json_bundle
simple_join_by_key
```

Start with `parallel + json_bundle` for fastest delivery.

## Multi-tool policy

Default:

```json
{
  "allowModelSelectedMultiTool": false,
  "preferCompositeCapability": true,
  "maxToolCallsPerTurn": 3
}
```

Phase 7 should not allow open-ended model-controlled multi-tool execution unless explicitly enabled.

## Answer composer integration

Composite result should include:

- parent capability key
- sub-capability envelopes
- row counts per sub-capability
- merged rows or JSON bundle
- execution metadata per call
- overall correlation ID

Raw JSON mode should return the full bundle after sensitivity masking.

Structured mode should summarize each section and optionally include tables per section.

## Acceptance criteria

- A composite capability can execute multiple read-only sub-capabilities.
- Sub-capability execution still goes through `CapabilityExecutionFacade`.
- Unauthorized sub-capabilities are blocked.
- Results can be returned as raw JSON bundle.
- Structured answer can summarize the composite result.
- The model does not freely choose arbitrary multi-tool chains in this phase.


---

<!-- 08_EVAL_OBSERVABILITY_AND_ROLLOUT.md -->

# Phase 8 — Evaluation, Observability, and Rollout

## Goal

Add regression tests, routing evaluation, telemetry, and rollout controls so the Agent Framework routing path can be deployed safely.

## Evaluation datasets

Create:

```text
tests/TILSOFTAI.Evals/tool-routing.vi.jsonl
tests/TILSOFTAI.Evals/tool-routing.en.jsonl
tests/TILSOFTAI.Evals/argument-extraction.vi.jsonl
tests/TILSOFTAI.Evals/argument-extraction.en.jsonl
tests/TILSOFTAI.Evals/answer-composer.jsonl
tests/TILSOFTAI.Evals/write-preview.jsonl
```

## Routing examples

Vietnamese:

```json
{
  "utterance": "Tồn mã MADEIRA-BLK ở kho BD còn bao nhiêu?",
  "locale": "vi-VN",
  "expectedDomain": "warehouse",
  "expectedFunction": "warehouse_inventory_by_item",
  "expectedArguments": {
    "item_no": "MADEIRA-BLK",
    "warehouse_code": "BD"
  },
  "shouldExecute": true
}
```

English:

```json
{
  "utterance": "Show open purchase orders for supplier ABC last month",
  "locale": "en-US",
  "expectedDomain": "purchasing",
  "expectedFunction": "purchasing_open_purchase_orders_by_supplier",
  "expectedArguments": {
    "supplier_code": "ABC",
    "date_range": "previous_month"
  },
  "shouldExecute": true
}
```

Write preview:

```json
{
  "utterance": "Tạo PO cho supplier ABC item A123 số lượng 500",
  "locale": "vi-VN",
  "expectedDomain": "purchasing",
  "expectedFunction": "purchasing_po_create_preview",
  "requiresConfirmation": true,
  "shouldExecuteWriteImmediately": false
}
```

Missing argument:

```json
{
  "utterance": "Xem công nợ khách hàng",
  "locale": "vi-VN",
  "expectedDomain": "accounting",
  "expectedFunction": "accounting_receivables_by_customer",
  "shouldExecute": false,
  "expectedClarification": true
}
```

## Metrics

Track:

```text
function_selection_accuracy
argument_extraction_accuracy
missing_required_argument_detection
ambiguous_entity_detection
false_read_execution
false_write_execution
unauthorized_execution
clarification_rate
answer_format_accuracy
latency_by_stage
adapter_failure_rate
```

Acceptance targets:

```text
function_selection_accuracy >= 90%
argument_extraction_accuracy >= 85%
missing_required_argument_detection >= 95%
false_write_execution = 0%
unauthorized_execution = 0%
```

## Telemetry and audit

Log:

```text
correlationId
tenantId
userId
locale
user message hash or redacted message
detected hard signals
candidate domains
candidate capabilities
advertised function tools
selected function
arguments before normalization
arguments after normalization
validation result
adapter type
row count
answer mode
latency per stage
model/provider
success/failure
error code
```

Secure audit/debug may include stored procedure name. Normal logs should not expose internal procedure names unless policy allows it.

Never log:

```text
secrets
credentials
full sensitive raw rows
unmasked financial/PII fields when policy forbids
```

## Rollout strategy

Use phased rollout:

```text
Stage 1: feature flag off by default, internal dev only
Stage 2: enable for one tenant and 5 priority read-only capabilities
Stage 3: enable for selected internal users
Stage 4: enable raw JSON and structured answer modes
Stage 5: enable write preview tools, but not write execution
Stage 6: enable confirmation + approval execution
Stage 7: expand domains and capabilities
```

## PR checklist

Before submitting changes:

- [ ] Feature flag off keeps legacy behavior.
- [ ] Candidate tool list is capped.
- [ ] No function tool directly calls SQL.
- [ ] Dynamic descriptions come from SQL KB.
- [ ] Domain retrieval works for Vietnamese and English.
- [ ] Required args missing returns clarification.
- [ ] Ambiguous entity returns user selection/follow-up.
- [ ] Read-only capability executes through facade.
- [ ] Write capability only produces preview before confirmation.
- [ ] Actual write execution requires approval.
- [ ] Answer composer supports `RawJson` and `Structured`.
- [ ] Sensitive fields are masked before AI summary.
- [ ] Tool routing trace is persisted.
- [ ] Eval tests added for priority domains.

## Final target state

```text
SQL Server 2025 Semantic KB
  -> multilingual domain/capability/argument retrieval
  -> dynamic function descriptions
  -> entity aliases and embeddings

Microsoft Agent Framework
  -> selected function tools only
  -> natural language to tool + params
  -> optional human-in-the-loop support

CapabilityExecutionFacade
  -> source-of-truth policy/validation/approval
  -> adapter execution boundary

AnswerComposer
  -> raw JSON OR structured answer
  -> text/table/chart/summary/follow-up/confirmation
```

End state: Microsoft Agent Framework is the brain for tool and parameter selection; SQL Server 2025 is the semantic memory/control plane; existing TILSOFTAI governance remains the execution safety boundary.
