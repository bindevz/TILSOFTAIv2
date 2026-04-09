# TILSOFTAI V3

TILSOFTAI is evolving from a module-centric, SQL-first orchestration runtime into a supervisor-driven internal AI platform.

Sprint 1 establishes the core V3 boundaries without pretending the full migration is done:
- `ISupervisorRuntime` is now the intended orchestration center.
- `IDomainAgent` and `IAgentRegistry` exist, with a `LegacyChatDomainAgent` bridging the current chat pipeline.
- `IToolAdapter` and `IToolAdapterRegistry` exist, with `SqlToolAdapter` wrapping the current `ISqlExecutor`.
- `IApprovalEngine` and `ApprovalEngine` now own the write-approval contract, while `ActionApprovalService` remains only as a compatibility facade.

## Current runtime shape

The active request path is now:

```text
API / Hub / OpenAI Surface
  -> IOrchestrationEngine (compatibility facade)
     -> ISupervisorRuntime
        -> IAgentRegistry
           -> LegacyChatDomainAgent
              -> ChatPipeline
                 -> IToolHandler
                    -> SqlToolAdapter / named handlers

Write requests
  -> IApprovalEngine
     -> IActionRequestStore
     -> SqlToolAdapter
```

That means the repository is still transitional. Domain behavior is not yet fully split into accounting, warehouse, sales, and other first-class agents, but the contracts for that split now exist in code.

## Sprint 1 scope

Done in this sprint:
- established supervisor, agent, tool adapter, and approval engine contracts
- mapped legacy chat orchestration through `SupervisorRuntime`
- mapped SQL execution through `SqlToolAdapter`
- switched API action approval endpoints to `IApprovalEngine`
- documented module-era paths that should be removed in later sprints

Explicitly not done yet:
- full multi-agent routing
- full replacement of module scope resolution
- non-SQL adapter implementations
- complete approval workflow redesign beyond the existing SQL-backed path

## Transitional compatibility

These paths still exist on purpose:
- `IOrchestrationEngine` and `OrchestrationEngine` keep current controllers and hubs stable while routing into `ISupervisorRuntime`
- `ActionApprovalService` remains for compatibility, but delegates to `IApprovalEngine`
- module loading, module scope resolution, and named tool-handler registration remain in place until capability-pack migration is ready

## Documentation

Sprint 1 notes live here:
- `docs/architecture_v3.md`
- `docs/migration_v2_to_v3.md`
- `docs/cleanup_report.md`

## License

Internal / proprietary.
