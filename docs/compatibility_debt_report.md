# Compatibility Debt Report - Sprint 19

This document tracks transitional components that still exist after Sprint 9, plus the components removed or reduced during the sprint.

## Removed In Sprint 9

| Component | Result | Notes |
|-----------|--------|-------|
| `LegacyChatPipelineBridge` | Deleted | Explicit legacy fallback no longer executes `ChatPipeline`; `GeneralChatAgent` returns `LEGACY_RUNTIME_RETIRED`. |
| `ChatPipeline` | Deleted | The legacy multi-step chat/tool owner is no longer registered or compiled. |
| `ChatRequest` / `ChatResult` | Deleted | These existed only for the retired pipeline path. |
| `ChatPipelineInstrumentation` | Deleted | Legacy pipeline span source removed from default runtime registration. |

## Changed In Sprint 9

| Component | Result | Notes |
|-----------|--------|-------|
| `GeneralChatAgent` | Supervisor-native only | Handles simple general requests and deterministic unsupported/retired legacy responses. |
| Capability catalog | Platform-owned | `catalog/platform-catalog.json` overrides static/bootstrap records. |
| External connection catalog | Platform-owned | Durable platform connection records override bootstrap configuration. |
| Capability contracts | Typed | Representative contracts now validate type, format, enum, and length/range rules. |
| Deep analytics E2E | External boundary | Marked `Category=ExternalDeepWorkflow`, owned by Analytics, and gated by `TEST_SQL_CONNECTION`. |

## Changed In Sprint 10

| Component | Result | Notes |
|-----------|--------|-------|
| Platform catalog mutation | Governed control plane | Catalog changes are submitted, independently reviewed, and applied through SQL-backed change requests. |
| Bootstrap fallback | Visible operational state | Startup logs, metrics, and `/health/ready` now report `platform`, `mixed`, `bootstrap_only`, or `empty`. |
| Catalog integrity | Enforced on load and mutation | Duplicate keys, unresolved REST connections, raw secrets, and missing contracts are reported as validation errors. |
| No-argument capabilities | Explicit contracts | Summary/list capabilities now reject unexpected arguments through no-argument contracts. |
| Module packages | Runtime-detached | Remaining package projects are solution-local compatibility artifacts and are not configured through API runtime. |

## Changed In Sprint 11

| Component | Result | Notes |
|-----------|--------|-------|
| Catalog mutation safety | Hardened | Expected-version checks, duplicate pending-change detection, preview, idempotent apply, and rollback reference metadata were added. |
| Production fallback | Stricter | Production config disables bootstrap fallback and marks mixed/bootstrap-only source modes unhealthy. |
| Approval policy | Environment/risk aware | Apply roles, high-risk approver roles, break-glass roles, and production-like independent apply are configured separately. |
| Contract lifecycle | Versioned baseline | `ArgumentContract` includes `ContractVersion`, `SchemaDialect`, and `SchemaRef` for future schema governance. |
| Module package end-state | Decided | Module packages are retained only as non-runtime packaging or diagnostic artifacts. |

## Changed In Sprint 12

| Component | Result | Notes |
|-----------|--------|-------|
| Catalog promotion | Gate-enforced | Production-like catalog rollout now evaluates source mode, preview validity, expected version, approved-change state, break-glass posture, and certification evidence. |
| Certification evidence | Durable | Evidence capture/listing is SQL-backed through `PlatformCatalogCertificationEvidence`. |
| Release observability | Extended | Promotion gate and certification evidence counters were added alongside SLO/alert/escalation definitions. |
| Emergency fallback | More contained | Mixed/bootstrap-only source modes and break-glass changes are blocked by release gates for production-like promotion. |

## Changed In Sprint 13

| Component | Result | Notes |
|-----------|--------|-------|
| Evidence trust | Hardened | Evidence must be verified and accepted before it satisfies production-like promotion policy. |
| Promotion history | Immutable manifest | Manifest identity and hash bind change ids, evidence ids, gate results, environment, and actors. |
| Rollout history | Append-only | Rollout state is captured as attestation records instead of mutable notes. |
| Audit review | Dossier-backed | Promotion dossiers make manifest/change/evidence/attestation lineage machine-readable. |

## Changed In Sprint 14

| Component | Result | Notes |
|-----------|--------|-------|
| Artifact trust | Provider-backed | Controlled artifact bytes can be read from a trusted root and compared with declared SHA-256. |
| Evidence policy | Tier-aware | Production-like policy can require `provider_verified` evidence. |
| Live certification | Freshness-aware | Required evidence kinds can expire by freshness window. |
| Audit retention | Policy-backed | Dossiers include retention snapshots and deterministic dossier hash. |

## Changed In Sprint 19

| Component | Result | Notes |
|-----------|--------|-------|
| Model module residue | Deleted | The obsolete Model module project and solution/API references were removed. |
| Multi-Agent ownership language | Clarified | Runtime ownership is Supervisor + Domain Agents + Tool Adapters; technical model/provider concerns are not domain ownership. |
| Module package classifications | Narrowed | Remaining package residue is limited to Platform packaging-only and Analytics diagnostic-only. |

## Removed In Sprint 20

| Component | Result | Notes |
|-----------|--------|-------|
| Module loader runtime | Deleted from API runtime | API startup no longer registers the loader, hosted service, activation provider, module options, or module health check. |
| API package references | Removed | The API project no longer references Platform or Analytics package projects. |
| Module runtime seed | Removed | `ModuleRuntimeCatalog` remains compatibility-only and no longer seeds enabled package rows. |
| Module scope resolver | Deleted | Prompt/tool/policy code now uses capability scope vocabulary; SQL keeps legacy parameter names only for compatibility. |

## Removed In Sprint 7

| Component | Result | Notes |
|-----------|--------|-------|
| `LegacyChatDomainAgent` | Deleted | Replaced by supervisor-native `GeneralChatAgent`. Unknown requests no longer proxy silently to `ChatPipeline`. |

## Changed In Sprint 7

| Component | Result | Notes |
|-----------|--------|-------|
| `DomainAgentRegistry` | General fallback only | Unmatched agent resolution now returns `general-chat` instead of `legacy-chat`. |
| Module health | Removed | Module health is not part of API runtime; use `native-runtime` and `platform-catalog`. |
| `RestJsonToolAdapter` | Production adapter path | Binding validation, retry, timeout, auth metadata, and classified failures are implemented. |
| Capability descriptors | Policy-aware | `RequiredRoles` and `AllowedTenants` gate native execution before adapter resolution. |

## Changed In Sprint 8

| Component | Result | Notes |
|-----------|--------|-------|
| `LegacyChatPipelineBridge` | Explicit legacy only for normal production routing | Unmatched warehouse/accounting requests now return `DOMAIN_CAPABILITY_NOT_FOUND` instead of invoking the bridge. |
| Module loader hosted service | Removed | The API no longer has an autoload path or `Modules` config section. |
| `NativeRuntimeHealthCheck` | Domain-agnostic | Uses all loaded capabilities and registered adapters rather than hardcoded warehouse/accounting checks. |
| `RestJsonToolAdapter` | Secret-governed | Uses external connection catalog and `ISecretProvider`; rejects raw secret metadata. |
| Capability descriptors | Contract-aware | Representative capabilities validate arguments before adapter resolution. |

## Removed In Sprint 6

| Component | Result | Notes |
|-----------|--------|-------|
| `IOrchestrationEngine` | Deleted | Edge entrypoints now call `ISupervisorRuntime` directly. |
| `OrchestrationEngine` | Deleted | Mapping moved to API edge code; exception/error handling remains in API middleware. |
| `ActionApprovalService` | Deleted | Approval callers use `IApprovalEngine` directly. |
| `ICapabilityPackProvider` | Deleted | No runtime references remained. |
| `ModuleBackedCapabilityPack` | Deleted | Dead module-era capability wrapper. |

## Remaining Compatibility Components

### 1. Legacy Capability-Scope SQL

| Field | Value |
|-------|-------|
| Status | Compatibility-only |
| Location | `sql/01_core/070_tables_module_scope.sql`, `sql/01_core/073_tables_runtime_policy.sql`, `sql/01_core/074_tables_react_followup_rule.sql` |
| Why it exists | Existing stored procedures and upgraded databases still use `ModuleKey` and `@ModuleKeysJson` names for capability-scope filtering. |
| What depends on it | Tool catalog, metadata dictionary, runtime policy, and ReAct follow-up compatibility procedures. |
| Removal condition | A future DB migration can rename columns/parameters after all deployed databases and clients stop depending on the legacy names. |

Current classifications:

| Package | Classification |
|---------|----------------|
| `TILSOFTAI.Modules.Platform` | solution-local compatibility package, not referenced by API |
| `TILSOFTAI.Modules.Analytics` | solution-local diagnostic package, not referenced by API |

### 2. Bootstrap Configuration Sources

| Field | Value |
|-------|-------|
| Status | Bootstrap fallback |
| Location | `ConfigurationCapabilitySource`, `ConfigurationExternalConnectionCatalog` |
| Why it exists | Local bootstrap and emergency fallback when durable catalog records are unavailable. |
| What depends on it | `CompositeCapabilityRegistry`, `CompositeExternalConnectionCatalog` |
| Removal condition | SQL/admin platform catalog write path and operational bootstrap process are fully established. |

### 3. InMemoryCapabilityRegistry

| Field | Value |
|-------|-------|
| Status | Test fixture |
| Location | `src/TILSOFTAI.Orchestration/Capabilities/InMemoryCapabilityRegistry.cs` |
| Why it exists | Unit and integration tests use constructor-driven capability sets. Production uses `CompositeCapabilityRegistry`. |
| Removal condition | None required; may become internal test support in a later cleanup. |

## Current Debt Priorities

Completed in code. Remaining priorities are live staging/prod-like certification evidence, optional signed bundle verification where required, operator training, and future cleanup of Platform/Analytics residue when packaging or diagnostic ownership no longer needs it. The removed Model module must not be reintroduced as a technical module or pseudo-agent.
