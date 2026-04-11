# Operational Runtime Observability - Sprint 8

Sprint 8 keeps the runtime signals and narrows bridge fallback to explicit legacy use.

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
- Bridge fallback should trend downward. After Sprint 8, normal unmatched warehouse/accounting requests should not use the bridge; a spike should mainly indicate explicit legacy fallback.
- `tilsoftai_runtime_adapter_failures_total` by `adapter=rest-json` or `adapter=sql` identifies integration boundaries causing failures.
- `CAPABILITY_ACCESS_DENIED` in adapter-failure labels means capability policy stopped execution before adapter resolution.
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

Bridge fallback reasons are now explicit:
- `no_capability_match`: domain agent could not resolve a native capability.
- `explicit_legacy_fallback`: request used `legacy-chat` or `legacyFallback=true`.
- `unsupported_general_request`: general agent declined a non-general unmatched request without invoking the bridge.

REST adapter failure codes:
- `REST_BINDING_INVALID`: endpoint/base URL/argument binding is invalid.
- `REST_CLIENT_ERROR`: final HTTP response was 4xx.
- `REST_SERVER_ERROR`: final HTTP response was 5xx.
- `REST_TRANSIENT_HTTP_ERROR`: final response was 408 or 429.
- `REST_TIMEOUT`: request exceeded configured timeout.
- `REST_TRANSPORT_ERROR`: network/transport failure after retries.
- `REST_SECRET_POLICY_VIOLATION`: raw secret-bearing metadata was rejected.
- `REST_SECRET_PROVIDER_UNAVAILABLE`: no platform secret provider was available for a secret reference.
- `REST_SECRET_NOT_FOUND`: configured secret reference could not be resolved.
- `REST_CONNECTION_NOT_FOUND`: configured `connectionName` was not present in the external connection catalog.
- `CAPABILITY_ARGUMENT_VALIDATION_FAILED`: capability arguments failed contract validation before adapter execution.

## Operational Triage

1. If user traffic succeeds but native counts are low, inspect bridge fallback reasons and capability resolution logs.
2. If native failure counts rise for one capability, inspect adapter failures for the same `capability` and `adapter` labels.
3. If approval execution failures rise, inspect write-action catalog, role validation, and `IWriteActionGuard` rejection logs.
4. If bridge duration is high, prioritize migrating that request class to a native capability or replacing the catch-all agent.

## Readiness

`/health/ready` includes `native-runtime`, not `modules`. The native check verifies supervisor runtime resolution, all loaded native capabilities, and registered adapters generically. Module health remains available as a legacy diagnostic check outside the ready tag. See `runtime_readiness.md`.
