# Sprint 35 Phase 0 - Architecture Freeze

Sprint 35 is a core runtime stabilization sprint.

Only the Model read-only domain is allowed in runtime.

Official Microsoft Agent Framework remains the only AI routing brain.

No non-model domain, real write action, or framework swap is allowed.

## Preflight Notes

- Runtime configuration in `src/TILSOFTAI.Api/appsettings.json`, `src/TILSOFTAI.Api/appsettings.Development.json`, and `src/TILSOFTAI.Api/appsettings.Local.example.json` already enables the official Microsoft Agent Framework route, disables legacy fallback, and limits allowed domains to `model`.
- `catalog/platform-catalog.json` is model-only, but single-model contracts still use `modelId`; this is reserved for Phase 2.
- `DomainGate` defaults to `model` and normalizes `product_model` to `model`.
- `SemanticCapabilityCandidateSelector` and `SemanticCapabilityRetriever` are wired through configured allowed domains and candidate limits.
- `OfficialAgentProviderFactory` creates an official `Microsoft.Agents.AI` agent from a registered `IChatClient`.
- `DynamicFunctionToolFactory` invokes capabilities through `CapabilityExecutionFacade`; function callbacks do not call SQL directly.
- Pending action storage exists, but its lifecycle and isolation fields are not yet Sprint 35-complete; this is reserved for Phase 5.

## Frozen Scope

- Runtime candidate domains stay restricted to `model`.
- Non-model capabilities must not be advertised to the agent.
- Real write execution remains disabled by default.
- Framework routing must stay on official Microsoft Agent Framework abstractions.
- Legacy keyword or regex routing must not become the final routing brain.
