# CTO_Action_Memo_Sprint_9

Repository: `bindevz/TILSOFTAIv2`  
Baseline reviewed against Sprint 8 commit: `7a9cfd83d2fac08223760a6f4ba9ea029b396ed9`

## Executive directive

Sprint 8 was a strong platform-governance sprint.

It materially improved enterprise readiness by:
- introducing governed external connection metadata,
- resolving external auth and API keys through a secret provider,
- rejecting raw secret-bearing capability metadata,
- adding contract-first capability validation,
- making native readiness domain-agnostic,
- reducing bridge fallback for warehouse/accounting agents,
- and proving a second governed REST-backed capability path.

However, Sprint 8 still does **not** complete the enterprise-grade journey.

The platform is now best described as:

> **enterprise-grade platform core with governed integrations and bounded legacy residue, but not yet fully enterprise-grade as a durable, de-legacy production platform**

Sprint 9 must therefore be a **legacy retirement + durable catalog + typed-contract hardening sprint**.

## CTO verdict from Sprint 8

### What Sprint 8 achieved correctly

1. External integration governance is materially stronger.
   `RestJsonToolAdapter` now resolves `AuthTokenSecret`, `ApiKeySecret`, and `HeaderSecrets` through `ISecretProvider`, sourced via `IExternalConnectionCatalog`.

2. Capability execution is now contract-aware.
   Representative capabilities use `ArgumentContract` and fail before adapter execution when validation fails.

3. Native readiness semantics are more honest.
   `NativeRuntimeHealthCheck` is domain-agnostic and validates loaded capabilities and adapters generically.

4. Bridge usage was reduced again.
   Unmatched warehouse/accounting requests now return `DOMAIN_CAPABILITY_NOT_FOUND` instead of silently falling into the bridge.

5. The adapter model is more credible beyond SQL.
   A second governed REST-backed capability now exists: `accounting.exchange-rate.lookup`.

6. Module legacy autoload is no longer the default.
   `ModuleLoaderHostedService` now starts only when `Modules:EnableLegacyAutoload=true`.

## Why Sprint 8 is still not fully enterprise-grade

1. `LegacyChatPipelineBridge` and `ChatPipeline` still exist as live runtime debt.
2. Module-era infrastructure still supports the legacy path when enabled.
3. Capability and connection definitions are still primarily configuration-backed rather than durable platform records.
4. Argument contracts remain shallow: required names / allowed names / object shape, but not typed or schema-level constraints.
5. One bounded deep analytics workflow test remains skipped.
6. SQL is still the dominant real production path.

## Sprint 9 mission statement

Sprint 9 must convert the platform from a **governed core with bounded legacy residue** into a more **durable, de-legacy enterprise platform**.

## Sprint 9 priorities

### Must fix in Sprint 9

#### 1. Replace `ChatPipeline` legacy tool catalog behavior
- replace legacy `ChatPipeline` tool-catalog behavior with capability-pack loading or equivalent supervisor-native compatibility support
- remove module-first tool discovery from the active compatibility story
- ensure any remaining compatibility flow does not depend on the historical module pipeline model

#### 2. Delete or neutralize `LegacyChatPipelineBridge`
- remove the bridge as a meaningful production dependency
- replace explicit legacy fallback with supervisor-native general workflow handling or deterministic unsupported paths
- do not introduce a new fallback layer

#### 3. Move capability and connection records to a durable platform catalog
- stop treating `appsettings.json` as the primary production catalog
- introduce a durable, admin-governed platform catalog for capabilities and external connections
- define explicit precedence between static defaults, bootstrap config, and durable platform records
- document audit/change-control expectations

#### 4. Deepen contracts from name validation to typed/schema validation
- extend representative capability contracts beyond required/allowed names
- validate types, formats, ranges, enum/domain rules where appropriate
- keep validation deterministic and operator-safe
- validate before adapter execution

#### 5. Reduce remaining module-era runtime registration
- remove or isolate default registration of module-era services where safe
- ensure default runtime boot is clearly supervisor-native and capability-native

#### 6. Resolve or formally isolate the skipped deep analytics boundary
- either restore the skipped deep analytics E2E workflow into supported validation,
- or move it into a separately-owned bounded validation suite with clear execution rules

### Should fix in Sprint 9

- improve admin/governance model for durable catalogs
- add one more non-SQL production path only if it directly strengthens platform maturity

### Can defer to Sprint 10

- broad planner/graph orchestration
- large multi-agent collaboration
- major UI/product expansion
- performance certification

## Definition of done

- `LegacyChatPipelineBridge` is deleted or no longer a meaningful runtime dependency
- `ChatPipeline` is deleted, replaced, or reduced to tightly bounded non-primary residue
- production capability and connection definitions are owned by a durable catalog rather than primarily by app configuration
- representative capabilities enforce typed/schema-style validation, not just name checks
- default runtime boot is no longer visibly module-era-centric
- the skipped deep analytics boundary is restored or formally isolated
- docs reflect runtime truth honestly

## Must-fail conditions

- `LegacyChatPipelineBridge` still matters for production runtime behavior
- `ChatPipeline` still owns the main compatibility execution story
- `appsettings.json` remains the primary production capability/connection catalog
- contract validation still stops at required/allowed argument name checks
- module-era services remain central to default runtime boot
- Sprint 9 adds abstractions without deleting meaningful legacy structures

## Required reporting format from the agent

1. Summary of Sprint 9 outcomes
2. Exact files created
3. Exact files modified
4. Exact files deleted
5. Bridge/ChatPipeline retirement changes
6. Durable catalog changes
7. Typed contract validation changes
8. Module-era runtime reductions
9. Validation boundary cleanup
10. Validation/test improvements
11. Remaining compatibility components
12. Remaining enterprise-grade blockers
13. Recommended Sprint 10 priorities

## Final CTO note

Sprint 8 proved the platform can govern integrations and reduce legacy dependence.
Sprint 9 must prove the platform can **retire** legacy runtime structures and **own production metadata durably**.
Do not spend Sprint 9 polishing the old compatibility story.
Spend it deleting it, replacing it where necessary, and moving production truth into durable platform-owned catalogs with stronger contract semantics.