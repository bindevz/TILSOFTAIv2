# CTO Action Memo — Sprint 5
Repository: `bindevz/TILSOFTAIv2`  
Baseline reviewed against Sprint 4 commit: `1fb4849c504b0c321ef9be346a51b2b7f38915b4`

## Executive directive

Sprint 4 proved that V3 is viable: the repository now has a real supervisor-driven runtime, a first domain-native Warehouse path, approval-governed write execution, and hardened auth behavior. Sprint 5 must convert that architectural proof into a broader, more credible platform foundation.

Sprint 5 is **not** the sprint for adding more orchestration complexity. It is the sprint for:
1. adding a second native domain path,
2. making capability resolution more reliable than keyword heuristics,
3. validating critical paths with infrastructure-level integration tests,
4. reducing compatibility debt without destabilizing the platform.

The single most important outcome of Sprint 5 is this:

> The platform must no longer look like “one native demo path plus legacy fallback everywhere else.”  
> It must begin to look like a repeatable pattern for multiple domains.

---

## CTO priorities

### Must fix in Sprint 5
1. **Implement the second native domain path**
   - Preferred target: `AccountingAgent` native read path.
   - Minimum acceptable outcome: at least one real accounting capability executes through capability resolution + adapter path without `LegacyChatPipelineBridge`.

2. **Replace fragile capability selection heuristics**
   - Current Warehouse capability resolution is too dependent on string matching.
   - Introduce a more reliable capability selection strategy:
     - structured request hints from supervisor, or
     - explicit capability request metadata, or
     - a domain-specific intent-to-capability resolver.
   - Keyword-only matching must stop being the long-term primary mechanism.

3. **Add infrastructure-backed integration tests**
   - Approval flow tests must go beyond in-memory behavior.
   - Native capability execution should be tested against real SQL-backed wiring or a realistic test harness.
   - Auth-enabled request path must be validated end-to-end.

4. **Reduce compatibility debt**
   - `LegacyChatPipelineBridge` must shrink in relevance, not grow.
   - `IOrchestrationEngine` must remain a compatibility facade only.
   - No new domain behavior may be added to legacy bridge code paths.

### Should fix in Sprint 5
1. **Move from static capability registry to data-driven registry**
   - Replace or extend `InMemoryCapabilityRegistry` with config-backed or SQL-backed capability registration.
   - Support explicit metadata for:
     - domain
     - adapter type
     - operation
     - target system
     - execution mode
     - policy metadata
     - argument schema or parameter contract

2. **Improve argument extraction for native paths**
   - Domain-native execution should not rely on minimal payload fallback only.
   - Add structured argument mapping for at least:
     - one warehouse capability
     - one accounting capability

3. **Increase observability by execution path**
   - Track native path vs bridge fallback path.
   - Emit domain, capability key, adapter type, duration, success/failure.
   - Add enough telemetry to answer:
     - how many requests still hit legacy bridge?
     - which capabilities are actually used?
     - which native paths fail most often?

4. **Clarify compatibility removal plan in docs**
   - Every remaining compatibility component must have:
     - why it still exists
     - what depends on it
     - what milestone removes it

### Can defer to Sprint 6
1. Non-SQL adapter implementation beyond contract maturity
2. Multi-agent collaboration
3. Planner / graph orchestration
4. Memory subsystem expansion
5. Full module-era removal
6. UI/product-facing workflow expansion

---

## Sprint 5 mission statement

Sprint 5 must prove that native execution is a repeatable platform pattern, not a single-domain exception.

---

## Sprint 5 goals

### Goal A — Second native domain path
Implement a real native read path for `AccountingAgent`.

Recommended capabilities:
- `accounting.receivables.summary`
- `accounting.payables.summary`
- `accounting.invoice.by-number`

Rules:
- read-only only in Sprint 5
- no direct domain-to-`SqlExecutor` coupling
- must execute through capability resolution + `IToolAdapterRegistry`
- fallback to `LegacyChatPipelineBridge` only when no native capability applies

### Goal B — Better capability resolution
Current native capability selection is too heuristic.

Required improvement:
- capability selection must become explicit enough to survive real usage
- acceptable approaches:
  - `CapabilityRequestHint` injected by `SupervisorRuntime`
  - `ICapabilityResolver` per domain
  - structured metadata attached to `AgentTask`
- unacceptable long-term approach:
  - continuing to rely mainly on string contains matching over capability key segments

### Goal C — Infrastructure-backed integration validation
Add tests that validate actual platform behavior, not just mocked contracts.

Required minimum test groups:
1. **Accounting native path integration**
2. **Warehouse native path integration**
3. **Approval lifecycle with real persistence boundary**
4. **Auth-enabled controller request path**
5. **Bridge fallback behavior remains controlled**

### Goal D — Capability registry maturity
Capability metadata must stop being purely in-memory static scaffolding.

Minimum acceptable Sprint 5 step:
- add a data-driven capability source
- support startup loading from configuration or SQL
- keep in-memory registry only as fallback/test fixture

### Goal E — Compatibility debt reduction
Compatibility components must lose centrality.

Required:
- no new runtime intelligence in `LegacyChatPipelineBridge`
- no new domain logic in `OrchestrationEngine`
- no expansion of module-centric patterns
- begin documenting removal conditions for:
  - `LegacyChatDomainAgent`
  - `LegacyChatPipelineBridge`
  - `IOrchestrationEngine` facade
  - module scope resolution dependencies

---

## Scope constraints

### Explicitly in scope
- one second native domain path
- better capability resolution
- data-driven capability registry step
- integration tests
- compatibility debt reduction
- documentation updates

### Explicitly out of scope
- adding new domain agents beyond existing set
- complex multi-agent orchestration
- planner framework
- full module removal
- broad REST/file/queue adapter buildout
- major API redesign
- domain write-native paths

---

## Architectural rules for the agent

1. Do not add new platform complexity unless it directly supports Sprint 5 goals.
2. Do not keep old and new runtime paths in parallel without justification.
3. Do not add accounting native execution by bypassing the capability/adapter pattern.
4. Do not increase reliance on `LegacyChatPipelineBridge`.
5. Do not couple domain agents directly to SQL-specific execution classes.
6. Do not weaken approval governance to speed up native execution work.
7. Do not leave tests purely mocked when the goal is infrastructure validation.
8. Do not leave README stale after changing runtime truth.

---

## Required deliverables

The agent must produce:

1. **Second native domain path**
   - preferably `AccountingAgent`
   - at least one real capability
   - tested

2. **Improved capability selection**
   - explicit resolver or structured hint mechanism
   - used by at least Warehouse and Accounting paths

3. **Capability registry upgrade**
   - config-backed or SQL-backed loading step
   - in-memory mode kept only as fallback/test utility

4. **Integration test suite additions**
   - not just unit tests
   - must validate real runtime wiring

5. **Compatibility debt report**
   - exact remaining transitional components
   - exact reason each remains
   - exact removal condition for each

6. **Documentation update**
   - README
   - architecture notes
   - Sprint 5 truth, not aspirational wording

---

## Definition of done

Sprint 5 is done only if all conditions below are met:

### Native execution
- `AccountingAgent` has at least one real native read path.
- `WarehouseAgent` native path still works and is not regressed.
- Both native paths execute through capability + adapter contracts.

### Capability resolution
- capability selection is more reliable than raw keyword heuristics
- the new mechanism is actually used in runtime, not just introduced as dead abstraction

### Registry maturity
- capabilities can be loaded from a data-driven source
- runtime no longer depends exclusively on hardcoded static capability lists

### Validation
- integration tests cover native path wiring
- integration tests cover approval lifecycle with realistic persistence boundaries
- auth-enabled controller path is validated

### Debt control
- legacy bridge did not gain new architectural weight
- compatibility components are clearly marked transitional
- no new module-centric runtime ownership was introduced

### Documentation
- README reflects Sprint 5 runtime truth
- current limitations are explicitly stated
- next-removal targets are documented

---

## Must-fail conditions

Sprint 5 must be considered incomplete if any of these remain true:

- only Warehouse has a native path
- AccountingAgent is still bridge-only
- capability resolution still depends mainly on naive string matching
- there is no infrastructure-backed validation
- capability registry remains static-only without any data-driven step
- compatibility debt grew instead of shrinking
- docs do not match runtime reality

---

## Suggested implementation order

1. Introduce capability resolver improvement
2. Add accounting capability definitions
3. Implement accounting native path
4. Introduce data-driven capability loading
5. Add integration tests
6. Tighten compatibility boundaries
7. Update README and architecture docs
8. Produce compatibility debt report

---

## Required reporting format from the agent

For Sprint 5 completion, the agent must report:

1. Summary of Sprint 5 outcomes
2. Exact files created
3. Exact files modified
4. Exact files deleted
5. Native paths now supported
6. Capability resolution changes
7. Data-driven registry changes
8. Integration tests added
9. Remaining compatibility components
10. Reason each remaining compatibility component still exists
11. Risks introduced
12. Recommended Sprint 6 priorities

---

## Final CTO note

Do not spend Sprint 5 polishing the appearance of V3.

Spend Sprint 5 proving that:
- native execution can scale to a second domain,
- capability resolution can become trustworthy,
- infrastructure behavior is testable,
- and compatibility debt is shrinking on purpose.
