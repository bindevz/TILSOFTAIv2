# CTO Action Memo — Sprint 6
Repository: `bindevz/TILSOFTAIv2`  
Baseline reviewed against Sprint 5 commit: `397e1f6c745d31b95438fdc7c582ec7897f0f0e6`

## Executive directive

Sprint 5 proved that the V3 model is repeatable: the platform now has two native domain paths, a structured capability resolver, a composite capability registry, and a clearer compatibility-debt map.

Sprint 6 must move the platform from **repeatable architecture** toward **enterprise-grade execution discipline**.

This sprint is not about adding more surface area.
It is about:
1. removing obsolete compatibility layers that still own too much runtime traffic,
2. replacing module-era runtime dependencies where they still distort architecture,
3. validating critical execution paths with more production-like integration behavior,
4. improving operational observability and failure diagnosability,
5. preserving strict write governance while reducing dead or transitional code.

The primary strategic outcome of Sprint 6 is this:

> The platform must stop looking like a modern core wrapped inside old entrypoints and old discovery patterns.  
> It must begin to look like a clean enterprise runtime with bounded compatibility residue.

---

## CTO priorities

### Must fix in Sprint 6

1. **Remove `IOrchestrationEngine` as the primary edge facade**
   - Controllers and hubs must migrate to `ISupervisorRuntime` directly.
   - `IOrchestrationEngine` / `OrchestrationEngine` should be deleted if all callers are migrated.
   - If a temporary adapter still remains, it must be minimal and no longer own behavior such as mapping, classification, or logging concerns that belong elsewhere.

2. **Remove `ActionApprovalService` compatibility facade**
   - Audit all references.
   - Migrate all remaining callers to `IApprovalEngine`.
   - Delete the facade if no references remain.

3. **Reduce module-era runtime ownership**
   - Module discovery / scope resolution must stop being required for native domain execution paths.
   - Begin the capability-pack migration in a concrete way:
     - native paths and adapter execution must not rely on module scope resolution,
     - module system should become bridge-only or compatibility-only,
     - health/runtime registration should clearly separate native capability execution from legacy module loading.

4. **Improve integration validation from “wiring-level” to “runtime-realistic”**
   - Add tests that use real HTTP request execution for authenticated API paths.
   - Add SQL-backed or realistic test harness validation for:
     - at least one native warehouse path,
     - at least one native accounting path,
     - approval persistence lifecycle.
   - Mock-heavy object graph tests remain useful, but they must no longer be the strongest proof point.

5. **Add execution-path observability**
   - The platform must emit and expose enough telemetry to answer:
     - how many requests used native execution?
     - how many requests fell back to bridge?
     - which capabilities are used most?
     - which capability/adapter combinations fail most often?
   - This must not be docs-only. Runtime instrumentation must exist.

### Should fix in Sprint 6

1. **Introduce first non-SQL adapter path**
   - At least one real non-SQL adapter implementation should exist.
   - Recommended targets:
     - REST adapter,
     - HTTP/JSON adapter,
     - gRPC adapter.
   - It does not need broad product coverage; it must prove the platform is not SQL-only in practice.

2. **Strengthen capability argument contracts**
   - Domain-native paths still need better argument extraction and validation.
   - Add explicit argument contracts or schemas for:
     - one warehouse capability,
     - one accounting capability,
     - one non-SQL capability if added.

3. **Harden fallback behavior**
   - Bridge fallback should be measurable, explicit, and increasingly rare.
   - Add policy or diagnostics for unsupported requests rather than silently letting bridge behavior mask architecture gaps.

4. **Tighten tenant and authorization enforcement**
   - Verify domain and capability execution can be constrained by tenant/role policy.
   - Add tests to prove unauthorized role/capability use fails predictably.

### Can defer to Sprint 7

1. Full removal of `LegacyChatPipelineBridge`
2. Full removal of `ChatPipeline`
3. General-purpose “chat/general agent” replacement for `LegacyChatDomainAgent`
4. Planner / graph orchestration
5. Multi-agent collaboration
6. Full non-SQL adapter suite
7. UI/product workflow expansion

---

## Sprint 6 mission statement

Sprint 6 must transform the platform from **architecturally promising** into **operationally credible**.

---

## Sprint 6 goals

### Goal A — Remove obsolete entrypoint facades
The edge layer must align with the real runtime.

Required:
- migrate `ChatController`, `OpenAiChatCompletionsController`, and `ChatHub` off `IOrchestrationEngine`
- inject and use `ISupervisorRuntime` directly
- relocate any remaining facade behavior into correct layers:
  - middleware
  - decorators
  - runtime services
- delete obsolete orchestration facade code if no callers remain

### Goal B — Remove obsolete approval facade
Required:
- audit all usage of `ActionApprovalService`
- replace with `IApprovalEngine`
- delete `ActionApprovalService` if the audit is complete
- update docs and compatibility debt report accordingly

### Goal C — Reduce module-system ownership
Required:
- native capability execution path must not depend on module scope resolution
- identify exactly which module-era components still matter only because of bridge fallback
- mark module-era components as:
  - native-runtime required
  - bridge-only required
  - dead / removable
- remove clearly dead module-era registrations where safe

Minimum acceptable Sprint 6 outcome:
- module system no longer appears as a platform-wide runtime dependency for native capability execution

### Goal D — Enterprise-grade integration validation
Required minimum:
1. authenticated API request integration test through ASP.NET pipeline
2. supervisor-to-agent-to-adapter integration using more realistic backing components
3. approval lifecycle validation with realistic persistence boundary
4. tenant/role failure-path validation
5. fallback-path observability validation

Preferred:
- use `WebApplicationFactory` or equivalent for HTTP-level tests
- use test configuration / test database / SQL container if feasible
- if SQL container is not feasible, use the strongest realistic test harness available and document the limitation honestly

### Goal E — Runtime observability
Required:
- emit structured telemetry for:
  - agent selected
  - execution path (`native`, `bridge`, `approval`)
  - capability key
  - adapter type
  - duration
  - success/failure
- add counters/metrics or equivalent instrumentation
- add at least one operational document explaining how to interpret these signals

### Goal F — First real non-SQL capability path
Required:
- add one capability that executes through a non-SQL adapter
- preferred domain:
  - warehouse external stock API
  - accounting invoice status API
  - general ERP connector demo capability
- must still go through:
  - capability registry
  - capability resolver
  - adapter registry
- do not bypass the platform pattern just to claim non-SQL support

### Goal G — Documentation and debt alignment
Required:
- update README to Sprint 6 truth
- update architecture docs
- update compatibility debt report
- add an enterprise readiness gap note describing what still blocks full enterprise-grade status

---

## Scope constraints

### Explicitly in scope
- removal of obsolete orchestration facade
- removal of obsolete approval facade
- module-era dependency reduction
- runtime observability
- realistic integration tests
- one non-SQL adapter path
- documentation and debt report updates

### Explicitly out of scope
- new domain agents
- broad new business domains
- planner / graph runtime
- multi-agent collaboration
- full bridge removal
- broad product UI work
- aggressive rewrite of all legacy subsystems at once

---

## Architectural rules for the agent

1. Do not add new abstraction layers unless they are used immediately in Sprint 6 runtime.
2. Do not preserve obsolete facades just because they were present before.
3. Do not add new domain logic to `LegacyChatPipelineBridge`.
4. Do not re-center architecture around module loading.
5. Do not add a non-SQL adapter outside the capability + adapter pattern.
6. Do not weaken approval governance.
7. Do not treat docs as completion without real runtime change.
8. Do not hide remaining debt; document it explicitly.
9. Prefer deleting dead code over wrapping it.
10. Keep the platform cohesive: supervisor owns orchestration, agents own domain execution, adapters own integration behavior.

---

## Required deliverables

The agent must produce:

1. **Edge facade removal**
   - controllers/hubs migrated to `ISupervisorRuntime`
   - obsolete orchestration facade removed or reduced to a trivial shim
   - file deletion if safe

2. **Approval facade removal**
   - all remaining approval callers use `IApprovalEngine`
   - obsolete approval facade removed if unused

3. **Module-dependency reduction**
   - clear separation between native capability runtime and bridge-only module usage
   - safe deletion or de-registration of dead compatibility pieces where possible

4. **Observability upgrade**
   - metrics / structured logs / tracing for execution path and capability usage
   - one operational doc for interpreting runtime telemetry

5. **Non-SQL adapter proof**
   - one real non-SQL adapter and one real non-SQL capability path

6. **Runtime-realistic integration tests**
   - authenticated HTTP path
   - native execution path
   - approval path
   - authorization failure path
   - fallback observability path

7. **Documentation**
   - README
   - architecture notes
   - compatibility debt report
   - enterprise readiness gap report

---

## Definition of done

Sprint 6 is done only if all conditions below are met:

### Runtime ownership
- edge entrypoints no longer primarily depend on `IOrchestrationEngine`
- approval callers no longer depend on `ActionApprovalService`
- native runtime ownership is clearer than in Sprint 5

### Debt reduction
- compatibility debt shrinks materially, not just in documentation
- at least one obsolete facade is fully removable or deleted
- module-era runtime influence is reduced in native execution paths

### Observability
- native vs bridge execution is measurable
- capability and adapter execution is measurable
- at least one runtime telemetry doc exists and reflects actual signals

### Validation
- HTTP-level authenticated integration test exists
- approval lifecycle has stronger proof than pure in-memory facade testing
- unauthorized or role-invalid execution paths are validated
- native domain paths still work and are not regressed

### Extensibility
- at least one non-SQL capability path exists through the adapter pattern
- platform no longer appears SQL-only in practice

### Documentation
- README reflects Sprint 6 truth
- compatibility debt report is updated
- enterprise-readiness blockers are stated clearly

---

## Must-fail conditions

Sprint 6 must be considered incomplete if any of these remain true:

- controllers and hubs still rely primarily on `IOrchestrationEngine`
- `ActionApprovalService` still exists only because nobody cleaned it up
- module-era runtime still appears central to native capability execution
- there is no real non-SQL adapter proof
- runtime observability still cannot distinguish native vs bridge paths
- tests remain mostly mock-based and not HTTP/runtime realistic
- compatibility debt is only re-documented, not reduced

---

## Suggested implementation order

1. Migrate controllers/hubs to `ISupervisorRuntime`
2. Audit and remove `ActionApprovalService`
3. Separate native runtime from bridge/module-era runtime registrations
4. Add execution-path telemetry
5. Add one non-SQL adapter and one non-SQL capability
6. Add HTTP-level and stronger integration tests
7. Delete or reduce dead code
8. Update README, architecture docs, compatibility debt report, readiness gap report

---

## Required reporting format from the agent

For Sprint 6 completion, the agent must report:

1. Summary of Sprint 6 outcomes
2. Exact files created
3. Exact files modified
4. Exact files deleted
5. Obsolete facades removed or reduced
6. Module-era components reduced or isolated
7. Telemetry / observability changes
8. Non-SQL adapter and capability paths added
9. Integration tests added
10. Remaining compatibility components
11. Reason each remaining compatibility component still exists
12. Enterprise-grade blockers still remaining
13. Recommended Sprint 7 priorities

---

## Final CTO note

Sprint 6 is not the sprint for new architectural theater.

Sprint 6 is the sprint for:
- deleting obsolete ownership,
- making runtime behavior measurable,
- proving the platform is not SQL-only,
- and forcing the codebase to look more like the architecture it claims to be.
