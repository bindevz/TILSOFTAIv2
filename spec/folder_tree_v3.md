# TILSOFTAI V3 Folder Tree Proposal

```text
src/
  TILSOFTAI.Api/
    Controllers/
    Hubs/
    Middlewares/
    Extensions/
    Contracts/
    Program.cs

  TILSOFTAI.Domain/
    ExecutionContext/
    Configuration/
    Errors/
    Security/
    Validation/
    Metrics/
    Telemetry/

  TILSOFTAI.Supervisor/
    ISupervisorRuntime.cs
    SupervisorRuntime.cs
    Routing/
    Classification/
    Decomposition/
    Composition/
    Policies/

  TILSOFTAI.Agents.Abstractions/
    IDomainAgent.cs
    IAgentRegistry.cs
    AgentTask.cs
    AgentResult.cs
    AgentExecutionContext.cs

  TILSOFTAI.Agents.Accounting/
    AccountingAgent.cs
    Policies/
    Capabilities/

  TILSOFTAI.Agents.Warehouse/
    WarehouseAgent.cs
    Policies/
    Capabilities/

  TILSOFTAI.Agents.Sales/
    SalesAgent.cs

  TILSOFTAI.Agents.Purchasing/
    PurchasingAgent.cs

  TILSOFTAI.Agents.MasterData/
    MasterDataAgent.cs

  TILSOFTAI.Tools.Abstractions/
    IToolAdapter.cs
    IToolAdapterRegistry.cs
    ToolExecutionRequest.cs
    ToolExecutionResult.cs
    CapabilityDefinition.cs
    ICapabilityResolver.cs

  TILSOFTAI.Tools.Sql/
    SqlToolAdapter.cs

  TILSOFTAI.Tools.Rest/
    RestApiToolAdapter.cs

  TILSOFTAI.Tools.FileImport/
    FileImportAdapter.cs

  TILSOFTAI.Tools.Queue/
    QueueActionAdapter.cs

  TILSOFTAI.Tools.Webhooks/
    WebhookAdapter.cs

  TILSOFTAI.Approvals/
    IApprovalEngine.cs
    ApprovalEngine.cs
    ProposedAction.cs
    ApprovalContext.cs
    ActionExecutionResult.cs
    Policies/
    Stores/

  TILSOFTAI.Infrastructure/
    Sql/
    Caching/
    Logging/
    Observability/
    Capabilities/
    Secrets/
    Modules/   # temporary compatibility only, remove later if no longer needed

  TILSOFTAI.Contracts/
    Api/
    Streaming/
    OpenAI/

  TILSOFTAI.Observability/
    Metrics/
    Audit/
    Tracing/
```
