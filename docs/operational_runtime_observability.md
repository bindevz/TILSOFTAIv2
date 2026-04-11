# Operational Runtime Observability - Sprint 6

Sprint 6 adds runtime signals for supervisor routing, native capability execution, bridge fallback, approval execution, capability invocation, adapter failures, and duration.

## Metrics

| Metric | Meaning | Important labels |
|--------|---------|------------------|
| `tilsoftai_runtime_supervisor_executions_total` | Supervisor requests completed. | `agent`, `domain`, `success` |
| `tilsoftai_runtime_native_executions_total` | Native domain-agent capability executions. | `agent`, `capability`, `adapter`, `success` |
| `tilsoftai_runtime_bridge_fallback_total` | Requests that fell back to the legacy bridge. | `agent`, `reason`, `success` |
| `tilsoftai_runtime_approval_executions_total` | Approval lifecycle operations. | `operation`, `adapter`, `success` |
| `tilsoftai_runtime_capability_invocations_total` | Capability invocations by key and adapter. | `agent`, `capability`, `adapter`, `success` |
| `tilsoftai_runtime_adapter_failures_total` | Adapter-level failures from native execution. | `agent`, `capability`, `adapter`, `error` |
| `tilsoftai_runtime_execution_duration_seconds` | Runtime duration histogram for supervisor, native, bridge, and approval paths. | `path` plus path-specific labels |

## How To Read The Signals

- Native execution should trend upward as more domain requests resolve to capabilities.
- Bridge fallback should trend downward. A spike in `tilsoftai_runtime_bridge_fallback_total` means classification or capability coverage is missing.
- `tilsoftai_runtime_adapter_failures_total` by `adapter=rest-json` or `adapter=sql` identifies integration boundaries causing failures.
- `tilsoftai_runtime_capability_invocations_total` shows which domain capabilities are actually used.
- `tilsoftai_runtime_approval_executions_total` confirms writes are still governed by `IApprovalEngine` and adapter-level guards.
- Duration histograms split by `path` let operations compare native, bridge, approval, and supervisor latency.

## Structured Log Events

Runtime instrumentation emits `RuntimeExecutionObserved` for supervisor, native, bridge, and approval paths. Adapter failures emit `RuntimeAdapterFailureObserved`.

Look for these fields:
- `Path`: `supervisor`, `native`, `bridge`, or `approval`
- `AgentId`
- `CapabilityKey`
- `AdapterType`
- `DurationMs`
- `Success`
- `Reason` for bridge fallback
- `ErrorCode` for adapter failures

## Operational Triage

1. If user traffic succeeds but native counts are low, inspect bridge fallback reasons and capability resolution logs.
2. If native failure counts rise for one capability, inspect adapter failures for the same `capability` and `adapter` labels.
3. If approval execution failures rise, inspect write-action catalog, role validation, and `IWriteActionGuard` rejection logs.
4. If bridge duration is high, prioritize migrating that request class to a native capability or replacing the catch-all agent.
