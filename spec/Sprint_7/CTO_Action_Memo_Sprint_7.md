# CTO_Action_Memo_Sprint_7

Repository: `bindevz/TILSOFTAIv2`  
Baseline reviewed against Sprint 6 commit: `af97a1bcf05ae5594cd2976aa1f77316d2baa841`

## Executive directive

Sprint 6 materially improved the platform:
- obsolete edge and approval facades were removed,
- runtime telemetry was introduced,
- the first non-SQL adapter-backed capability path now exists,
- and the project documents its remaining enterprise-readiness blockers.

Sprint 7 must convert this platform from **enterprise-ready foundation** into a substantially more **enterprise-grade operating core**.

Sprint 7 is not a feature-expansion sprint.

It is a **hardening, debt-burndown, and productionization sprint** focused on:
1. reducing the legacy fallback footprint,
2. removing module-era runtime influence from the active system,
3. productionizing the non-SQL adapter path,
4. making validation and test confidence credible at enterprise scale,
5. strengthening policy enforcement and operational controls.

The primary strategic outcome of Sprint 7 is this:

> The platform must stop depending on legacy catch-all execution as a normal safety net.  
> It must evolve into a platform where native execution is the expected path, legacy fallback is exceptional, and external integrations are governed like production systems.

---

## CTO verdict from Sprint 6

The repository is now:
- architecturally credible,
- operationally improving,
- and increasingly aligned with a supervisor + agents + adapters + approval model.

However, it is **not fully enterprise-grade yet** because the following blockers remain:
- `LegacyChatDomainAgent` still owns catch-all fallback behavior.
- `LegacyChatPipelineBridge` and `ChatPipeline` still carry meaningful runtime load.
- module loading / module scope resolution still survive in the legacy execution path and health/runtime behavior.
- REST/JSON capability support is still a proof path, not yet a production-governed external integration boundary.
- the full suite is not fully green due to analytics regressions and integration dependency friction.
- SQL remains the dominant real production path even though the adapter pattern now exists beyond SQL.
- readiness and health do not yet cleanly separate native platform readiness from legacy fallback readiness.

These blockers were already documented by the repository in Sprint 6 and must now be treated as execution items rather than documentation items.

---

## Sprint 7 mission statement

Sprint 7 must make the platform **operationally credible under enterprise expectations**, not just architecturally elegant.

---

## Sprint 7 priorities

### Must fix in Sprint 7

#### 1. Replace `LegacyChatDomainAgent` with a supervisor-native general/chat agent
The current catch-all fallback agent is still a major enterprise blocker.

Required:
- introduce a purpose-built supervisor-native general/chat agent
- make it the fallback for non-domain or cross-domain conversational requests
- do not let the general/chat agent simply proxy blindly to `LegacyChatPipelineBridge`
- define explicit ownership boundaries:
  - general conversational intent
  - unsupported request handling
  - native-or-bridge decision policy
- keep bridge fallback as a last-resort mechanism only

Success condition:
- `LegacyChatDomainAgent` is deleted or reduced to a trivial temporary shim with explicit removal condition

#### 2. Reduce `LegacyChatPipelineBridge` traffic and centrality
Bridge fallback must become measurably exceptional.

Required:
- add bridge usage thresholds or diagnostics
- add fallback reason classification with enough detail for operations
- reduce the classes of requests that still reach bridge fallback
- prevent silent architectural masking by bridge fallback

Success condition:
- bridge fallback is now clearly bounded, measured, and reduced in scope

#### 3. Replace module-era tool discovery for the legacy path
Module-era runtime influence is still too high for an enterprise-grade platform.

Required:
- introduce capability-pack or equivalent legacy-tool loading mechanism that does not depend on module scope ownership
- move legacy tool registration away from module-first runtime assumptions
- isolate or delete dead module-era registrations
- reduce module health from platform-wide readiness signal to legacy-only diagnostic if still needed

Success condition:
- module loading is no longer perceived as a platform runtime primitive
- it is either bridge-only support or removed from the main readiness story

#### 4. Productionize the non-SQL adapter path
`RestJsonToolAdapter` is currently a proof path. Sprint 7 must make it enterprise-governed.

Required:
- move REST endpoint binding fully into configuration or connection catalog style metadata
- add explicit timeout policy
- add retry policy or resilience policy
- add endpoint auth/header policy support
- add failure classification suitable for operational triage
- validate tenant/correlation/header propagation for external calls

Success condition:
- non-SQL integration no longer looks like a demo adapter; it looks like a governed external integration boundary

#### 5. Fix test suite credibility
A platform cannot claim enterprise-grade maturity with known unresolved test regressions and dependency mismatch warnings.

Required:
- resolve analytics-related failing tests
- resolve renderer/formatting regressions if still failing
- resolve IdentityModel version mismatch or isolate the affected integration project cleanly
- restore a credible “green enough” baseline for the platform

Success condition:
- the repository has a documented and reproducible green validation baseline
- known failures are either fixed or isolated with explicit reason and boundary

### Should fix in Sprint 7

#### 6. Separate native readiness from legacy fallback readiness
Health should reflect architectural truth.

Required:
- distinguish native runtime readiness from legacy fallback subsystem readiness
- avoid letting module-era diagnostics appear equivalent to native runtime health
- update health docs and operational runbooks

#### 7. Strengthen authorization and policy enforcement by capability
The platform already has auth and write governance; now it needs stronger execution policy boundaries.

Required:
- capability execution must be constrainable by tenant, role, and optional system policy
- add validation for capability access denial
- add tests for unauthorized capability execution attempts

#### 8. Strengthen argument contracts for native and external capabilities
Argument handling is still too lightweight for robust enterprise behavior.

Required:
- define stronger argument shape for:
  - at least one accounting capability
  - at least one warehouse SQL capability
  - the REST-backed external stock capability
- add validation errors that are deterministic and operator-friendly

### Can defer to Sprint 8

1. Full removal of `LegacyChatPipelineBridge`
2. Full removal of `ChatPipeline`
3. Large-scale multi-agent collaboration
4. Planner / graph execution
5. Broad adapter ecosystem rollout
6. Product workflow expansion beyond current scope
7. Performance/load benchmarking at platform-wide scale

---

## Sprint 7 goals

### Goal A — Native fallback modernization
Replace `LegacyChatDomainAgent` with a general/chat agent that belongs to the supervisor-driven architecture.

### Goal B — Legacy path containment
Reduce and classify bridge fallback so it becomes a controlled exception path.

### Goal C — Module-era runtime extraction
Replace module-first legacy tool discovery with capability-pack style legacy support, or isolate module system as legacy-only.

### Goal D — External integration productionization
Convert REST adapter support from proof path into governed external adapter behavior.

### Goal E — Enterprise validation baseline
Restore a trustworthy green baseline for tests and integration validation.

### Goal F — Policy and readiness refinement
Strengthen capability access control and separate native readiness from legacy readiness.

---

## Scope constraints

### Explicitly in scope
- replacement or reduction of `LegacyChatDomainAgent`
- bridge fallback reduction and classification
- module-era runtime extraction
- non-SQL adapter productionization
- validation baseline repair
- policy enforcement improvements
- health/readiness separation
- documentation updates

### Explicitly out of scope
- new business domains
- new domain agents beyond a general/chat agent replacement
- planner/graph runtime
- full bridge deletion
- full `ChatPipeline` deletion
- broad product/UI work
- large-scale workflow additions

---

## Architectural rules for the agent

1. Do not add new fallback layers.
2. Do not preserve `LegacyChatDomainAgent` just because it currently works.
3. Do not keep module loading in the main runtime narrative.
4. Do not treat REST adapter proof behavior as sufficient for enterprise-grade external integration.
5. Do not add complexity unless it directly removes debt or hardens production behavior.
6. Do not weaken approval or write governance.
7. Do not leave failing tests undocumented or unexplained.
8. Prefer replacement and deletion over one more compatibility wrapper.
9. Keep runtime ownership explicit: supervisor, agents, adapters, approvals, policy.
10. Keep legacy support bounded, measured, and removable.

---

## Required deliverables

1. **General/chat agent modernization**
   - replacement or strong reduction of `LegacyChatDomainAgent`
   - explicit supervisor-native general/chat ownership
   - file deletion if safe

2. **Bridge fallback containment**
   - better fallback reason classification
   - reduced scope of bridge fallback
   - operational thresholds or diagnostics

3. **Module-era extraction**
   - capability-pack or equivalent legacy tool discovery mechanism
   - reduced module-first ownership
   - health separation for legacy diagnostics

4. **REST adapter productionization**
   - config-driven endpoint governance
   - timeout/retry/auth/header policy support
   - stronger external error classification
   - production-like integration tests

5. **Validation baseline repair**
   - analytics and renderer regressions fixed or isolated
   - dependency/version mismatch addressed
   - documented green baseline

6. **Capability policy enforcement**
   - tenant/role/capability denial path
   - deterministic unauthorized execution behavior
   - tests added

7. **Documentation**
   - README
   - architecture notes
   - compatibility debt report
   - enterprise readiness gap report
   - readiness/health note for native vs legacy paths

---

## Definition of done

Sprint 7 is done only if all conditions below are met:

### Fallback modernization
- `LegacyChatDomainAgent` is deleted, replaced, or reduced to trivial residue
- general/chat fallback behavior is now supervisor-native

### Legacy containment
- bridge fallback is reduced and operationally classified
- bridge traffic is no longer just “whatever native did not handle”

### Module extraction
- module loader and scope resolution no longer appear as active runtime ownership for the platform
- native readiness is not tied to legacy module diagnostics

### External integration maturity
- non-SQL adapter path is production-governed via config/policy/resilience
- external call behavior is observable and operator-friendly

### Validation credibility
- green baseline is materially stronger than after Sprint 6
- known regressions are fixed or isolated with explicit rationale

### Access control
- capability-level authorization failures are tested and deterministic

### Documentation
- Sprint 7 docs match runtime reality
- enterprise blockers still remaining are explicit and honest

---

## Must-fail conditions

Sprint 7 must be considered incomplete if any of these remain true:
- `LegacyChatDomainAgent` remains unchanged as the catch-all fallback
- module system still appears central to the runtime story
- REST adapter still looks like a demo path with hardwired proof binding behavior
- analytics/test regressions remain unaddressed and undocumented
- readiness still conflates native runtime health with legacy fallback health
- capability-level denial paths are not validated
- Sprint 7 adds abstraction but does not materially shrink legacy influence

---

## Suggested implementation order

1. Replace or reduce `LegacyChatDomainAgent`
2. Improve bridge fallback classification and diagnostics
3. Introduce capability-pack style legacy tool discovery or isolate module loading as legacy-only
4. Productionize `RestJsonToolAdapter`
5. Add policy-enforced capability denial tests
6. Repair or isolate failing test baseline issues
7. Update readiness, debt, and enterprise gap docs
8. Delete dead code

---

## Required reporting format from the agent

1. Summary of Sprint 7 outcomes
2. Exact files created
3. Exact files modified
4. Exact files deleted
5. General/chat agent changes
6. Bridge fallback reductions
7. Module-era components reduced or isolated
8. REST adapter productionization changes
9. Validation baseline improvements
10. Capability-policy enforcement changes
11. Remaining compatibility components
12. Remaining enterprise-grade blockers
13. Recommended Sprint 8 priorities

---

## Final CTO note

Sprint 7 is the sprint where the platform must start behaving like a system operations teams can trust.

Do not spend Sprint 7 making the architecture more impressive on paper.
Spend it making the fallback story smaller, the integration story safer, the validation story stronger, and the runtime ownership cleaner.
