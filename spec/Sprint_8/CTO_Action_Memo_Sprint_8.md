# CTO_Action_Memo_Sprint_8

Repository: `bindevz/TILSOFTAIv2`  
Baseline reviewed against Sprint 7 commit: `5537b18116eb1a3715d216fd3d741cfab8bbe579`

## Executive directive

Sprint 7 was a real hardening sprint.

It materially improved enterprise readiness by:
- deleting the legacy catch-all chat agent,
- introducing a supervisor-native general agent,
- enforcing capability policy before native adapter execution,
- productionizing the first REST/JSON adapter path,
- separating native readiness from legacy diagnostic health,
- and restoring a credible green validation baseline.

This is meaningful progress.

However, Sprint 7 did **not** fully complete the enterprise-grade journey.

The platform is now best described as:

> **enterprise-grade core for bounded scope, but not yet enterprise-grade as a fully de-risked platform runtime**

Why this distinction matters:
- Native runtime ownership is now substantially cleaner.
- But legacy bridge/module infrastructure still exists as a real execution dependency.
- External integration governance is improved, but secret/config governance is not yet platform-grade.
- Capability policy exists, but contracts and policy depth are still too lightweight for long-term enterprise scaling.
- Health/readiness is cleaner, but still partially domain-hardcoded rather than platform-generalized.

Sprint 8 must turn this into a more complete enterprise-grade platform posture.

Sprint 8 is therefore a **platform de-legacy + governance + contract-hardening sprint**.

It is not a feature sprint.

---

## CTO verdict from Sprint 7

### What Sprint 7 achieved correctly

#### 1. Legacy catch-all fallback ownership improved materially
`LegacyChatDomainAgent` was removed and replaced by a supervisor-native `GeneralChatAgent`.

That is a real architectural improvement.

The platform no longer relies on a legacy catch-all agent as the default identity of unclassified requests.

#### 2. Capability execution is now policy-gated
`CapabilityDescriptor` now supports:
- `RequiredRoles`
- `AllowedTenants`

and policy is evaluated before adapter resolution.

That is an important step toward enterprise execution governance.

#### 3. REST adapter path is no longer proof-only
`RestJsonToolAdapter` now supports:
- configuration-driven binding,
- timeout,
- retry,
- auth token and API key metadata,
- tenant/correlation propagation,
- and classified failure handling.

This is no longer a toy proof.

#### 4. Native readiness is separated from legacy diagnostics
`/health/ready` now uses `NativeRuntimeHealthCheck`, while module health is legacy diagnostic only.

This is operationally correct and aligns health with architecture.

#### 5. Validation credibility improved
Sprint 7 documentation and tests indicate a green unit + integration baseline with one intentionally bounded skipped deep analytics E2E test.

This is a large improvement over Sprint 6.

---

## Why Sprint 7 is still not fully enterprise-grade

### 1. Bridge fallback still exists as a live execution path
`LegacyChatPipelineBridge` and `ChatPipeline` still matter.

That means the platform still has a real legacy execution substrate behind native routing.

As long as important unmatched request classes still reach the bridge, enterprise-grade status remains partial.

### 2. Module-era runtime infrastructure still exists in startup and legacy support
`IModuleLoader`, `ModuleLoaderHostedService`, `IModuleScopeResolver`, and related module-era behavior still exist for:
- `ChatPipeline`
- legacy tool catalog behavior
- diagnostic module health

That means module-era compatibility has not yet been extracted from the runtime footprint.

### 3. Secret sourcing is not yet enterprise-governed
REST auth/header support is present, but secret sourcing is still metadata/config responsibility.

This is not sufficient for enterprise-grade external integration governance.

Secrets must come from a platform secret provider or governed connection catalog.

### 4. Capability contracts are still too lightweight
The platform still primarily relies on dictionary/string-style argument extraction.

Enterprise-grade systems need:
- schema-bound request contracts,
- deterministic validation,
- operator-friendly error semantics,
- and testable compatibility boundaries.

### 5. General chat remains intentionally bounded, but too thin for durable production fallback
`GeneralChatAgent` is a good containment move, but it is still intentionally narrow:
- simple general-chat recognition,
- deterministic unsupported response,
- explicit legacy fallback when requested.

This is better than the old catch-all legacy agent, but it is not yet a mature supervisor-native general orchestration capability.

### 6. Native readiness is cleaner, but not yet generalized
`NativeRuntimeHealthCheck` is still domain-aware in a hardcoded way.

That is acceptable for Sprint 7, but not for a platform claiming scalable enterprise-grade readiness.

### 7. SQL remains the dominant production path
The adapter model is now real, but most real capability coverage is still SQL-backed.

Enterprise-grade platform maturity improves when at least one more production-style external integration path is governed and proven end-to-end.

---

## Sprint 8 mission statement

Sprint 8 must make the platform behave like a **governed native platform with shrinking legacy residue**, not merely a cleaner architecture with bounded debt.

---

## Sprint 8 priorities

### Must fix in Sprint 8

#### 1. Remove module-era tool catalog ownership from the runtime story
Required:
- replace module-first legacy tool catalog behavior with capability-pack or equivalent platform-native legacy support
- remove module scope resolution from active runtime ownership where safe
- reduce or eliminate module loader startup dependency if bridge path still remains
- ensure legacy support can survive without the module system defining platform identity

Success condition:
- module-era infrastructure is clearly isolated, optional, or removable without destabilizing native runtime

#### 2. Shrink `LegacyChatPipelineBridge` request classes again
Required:
- identify exactly which request categories still hit the bridge
- remove at least one meaningful bridge-bound request class through native handling
- reduce bridge fallback to narrower, more explicit execution reasons
- add operational reporting that makes bridge retirement measurable

Success condition:
- bridge traffic is smaller in scope than Sprint 7 and closer to controlled deprecation

#### 3. Move capability definitions toward durable governed sources
Required:
- reduce reliance on static fallback capability definitions for production behavior
- support durable configuration / connection catalog / capability source patterns
- document ownership and precedence clearly between static defaults and environment/runtime configuration

Success condition:
- production capability shape is governed by durable sources, not primarily by code literals

#### 4. Introduce production-grade secret sourcing for external integrations
Required:
- remove plain auth token / API key assumptions from capability metadata patterns
- integrate REST auth/header material through a secret provider or governed connection catalog
- ensure logs/telemetry never leak secret-bearing values
- add tests for secret-backed external execution configuration

Success condition:
- external adapter auth is platform-governed, not caller-assembled

#### 5. Add contract-first validation for capabilities
Required:
- introduce schema validation or strongly defined request contracts for:
  - at least one warehouse SQL capability
  - at least one accounting SQL capability
  - the REST stock capability
- return deterministic validation failure codes
- ensure validation happens before adapter execution
- document argument contracts

Success condition:
- native execution is no longer primarily dictionary/string driven for production-critical capability paths

#### 6. Generalize native readiness
Required:
- evolve `NativeRuntimeHealthCheck` away from warehouse/accounting hardcoding
- base readiness on loaded native capability sets and registered adapters generically
- distinguish:
  - native orchestration readiness
  - native capability availability
  - external adapter readiness
- keep legacy diagnostics separate

Success condition:
- readiness scales with platform growth instead of current domain assumptions

### Should fix in Sprint 8

#### 7. Deepen capability policy beyond role/tenant-only checks
Required:
- introduce optional system policy/provider-based checks
- support deny-by-policy scenarios beyond simple tenant/role matching
- add policy decision observability
- add tests for policy denial reasons

#### 8. Prove one more governed non-SQL production path
Required:
- add one more production-style external capability path
- prefer a second REST integration or a gRPC-backed capability
- include real governance:
  - connection metadata
  - secret sourcing
  - timeout/retry policy
  - failure classification
  - tests

#### 9. Reduce skipped deep analytics validation boundary
Required:
- either unskip the remaining deep analytics E2E test,
- or isolate it into a clearly bounded external workflow validation suite with ownership rules

### Can defer to Sprint 9

1. Full deletion of `ChatPipeline`
2. Full deletion of `LegacyChatPipelineBridge`
3. Large planner / graph runtime
4. Broad multi-agent collaboration
5. Cross-domain workflow planning
6. Broad capability marketplace
7. Large-scale performance/load certification

---

## Sprint 8 goals

### Goal A — Module-era extraction
Remove module-era tool catalog ownership from the platform runtime identity.

### Goal B — Bridge retirement progress
Shrink the legacy bridge again and make its remaining footprint explicitly temporary.

### Goal C — Governed capability sourcing
Move production capability definitions toward durable platform-governed sources.

### Goal D — Secret and connection governance
Make external integration secrets and auth fully platform-governed.

### Goal E — Contract-first native execution
Add strong validation and deterministic contracts for production capability paths.

### Goal F — Readiness generalization
Make native readiness generic, scalable, and aligned with platform growth.

### Goal G — External platform proof
Demonstrate a second governed non-SQL path to prove the adapter platform beyond SQL-plus-one.

---

## Scope constraints

### Explicitly in scope
- legacy bridge reduction
- module-era extraction/isolation
- governed capability sourcing
- secret provider / connection catalog integration
- capability contract validation
- readiness generalization
- one more governed external capability path
- policy depth improvements
- validation and docs updates

### Explicitly out of scope
- broad new business domains
- full planner runtime
- graph orchestration
- major UI/product expansion
- large-scale multi-agent choreography
- full platform-wide performance certification
- broad workflow feature expansion

---

## Architectural rules for the agent

1. Do not add new fallback layers.
2. Do not re-centralize the runtime around module loading.
3. Do not preserve bridge behavior without measuring and shrinking it.
4. Do not treat static code-defined capability descriptors as sufficient production governance.
5. Do not leave external secrets in raw metadata/config patterns.
6. Do not add schema/validation complexity unless it materially improves safety and determinism.
7. Do not weaken approval or write governance.
8. Prefer deletions, extraction, and ownership simplification over wrapper layering.
9. Keep native runtime identity explicit: supervisor, agents, capability registry, adapter registry, approval engine, policy.
10. Keep legacy support bounded, measurable, and removable.

---

## Required deliverables

1. **Module-era extraction**
   - capability-pack or equivalent replacement for legacy module-first tool catalog behavior
   - reduced startup/runtime dependency on module-era services
   - updated legacy diagnostic boundaries

2. **Bridge reduction**
   - narrower request classes reaching `LegacyChatPipelineBridge`
   - explicit measurement and reporting of remaining bridge reasons
   - updated debt report

3. **Governed capability sourcing**
   - durable capability source improvements
   - documented source precedence
   - reduced dependence on code literals for production capability shape

4. **Secret and connection governance**
   - secret-backed REST auth/header sourcing
   - safe telemetry/logging rules
   - tests for secret-backed integration config

5. **Capability contracts**
   - schema or strongly defined argument contracts
   - deterministic validation failures
   - tests covering invalid argument shapes

6. **Generic readiness**
   - domain-agnostic native readiness
   - clearer readiness segmentation
   - docs/runbooks updated

7. **Second governed external path**
   - one more external capability path
   - governance + observability + tests

8. **Documentation**
   - README
   - architecture notes
   - compatibility debt report
   - enterprise readiness gap report
   - readiness documentation
   - external integration governance note

---

## Definition of done

Sprint 8 is done only if all of the following are true:

### Module extraction
- module-era services no longer define active runtime identity
- module behavior is isolated to bounded compatibility concerns or reduced further

### Bridge reduction
- bridge fallback scope is smaller than Sprint 7
- remaining bridge request classes are explicitly documented and measurable

### Capability governance
- production capability sourcing is more durable than static fallback definitions
- source precedence is explicit and testable

### Secret governance
- external auth material is sourced through a governed platform mechanism
- secret-bearing data is not treated like ordinary metadata

### Contract maturity
- at least three representative capabilities have deterministic request validation
- invalid requests fail before adapter invocation

### Readiness
- native readiness is generalized beyond warehouse/accounting hardcoding
- native, legacy, and external readiness are clearly separated

### External proof
- a second governed non-SQL path exists and is tested

### Documentation
- docs describe runtime truth, not target-state aspiration

---

## Must-fail conditions

Sprint 8 must be considered incomplete if any of these remain true:
- module loader/scope resolver still appear central to runtime identity
- bridge fallback footprint is unchanged from Sprint 7
- capability definitions are still primarily code-defined for production behavior
- external auth secrets remain raw metadata values
- argument validation remains mostly dictionary/string-based
- readiness is still hardcoded around current domains
- Sprint 8 adds complexity without materially shrinking legacy influence or improving governance

---

## Suggested implementation order

1. Introduce governed secret/connection sourcing for REST integrations
2. Add contract-first validation for representative capabilities
3. Generalize native readiness
4. Replace or isolate module-first legacy tool catalog behavior
5. Remove one meaningful bridge-bound request class
6. Add one more governed external capability path
7. Update docs, debt, and readiness reports
8. Delete dead compatibility code

---

## Required reporting format from the agent

1. Summary of Sprint 8 outcomes
2. Exact files created
3. Exact files modified
4. Exact files deleted
5. Module-era components reduced or isolated
6. Bridge fallback reductions
7. Capability source governance changes
8. Secret/connection governance changes
9. Capability contract validation changes
10. Native readiness generalization changes
11. Second external capability path changes
12. Validation/test improvements
13. Remaining compatibility components
14. Remaining enterprise-grade blockers
15. Recommended Sprint 9 priorities

---

## Final CTO note

Sprint 7 proved the platform can harden.

Sprint 8 must prove the platform can govern itself:
- fewer legacy dependencies,
- safer external integrations,
- stronger contracts,
- better readiness semantics,
- and clearer production ownership.

Do not spend Sprint 8 making the system look more abstract.

Spend it making the platform safer, more governable, less legacy-dependent, and more scalable under enterprise operating conditions.
