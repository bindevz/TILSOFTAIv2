# Operational Runtime Observability - Sprint 11

Sprint 9 removes the legacy bridge/ChatPipeline execution path. Bridge metrics remain as historical instrumentation and to record explicit retired-legacy attempts, but there is no production bridge executor.

## Metrics

| Metric | Meaning | Important labels |
|--------|---------|------------------|
| `tilsoftai_runtime_supervisor_executions_total` | Supervisor requests completed. | `agent`, `domain`, `success` |
| `tilsoftai_runtime_native_executions_total` | Native domain-agent capability executions. | `agent`, `capability`, `adapter`, `success` |
| `tilsoftai_runtime_bridge_fallback_total` | Retired legacy fallback attempts or historical bridge observations. | `agent`, `reason`, `success` |
| `tilsoftai_runtime_approval_executions_total` | Approval lifecycle operations. | `operation`, `adapter`, `success` |
| `tilsoftai_runtime_capability_invocations_total` | Capability invocations by key and adapter. | `agent`, `capability`, `adapter`, `success` |
| `tilsoftai_runtime_adapter_failures_total` | Adapter-level failures from native execution. | `agent`, `capability`, `adapter`, `error` |
| `tilsoftai_runtime_execution_duration_seconds` | Runtime duration histogram for supervisor, native, bridge, and approval paths. | `path` plus path-specific labels |
| `tilsoftai_platform_catalog_source_mode_total` | Startup catalog source-of-truth report. | `mode`, `environment`, `production_like`, `platform_valid` |
| `tilsoftai_platform_catalog_mutations_total` | Catalog control-plane preview/submit/review/apply operations. | `operation`, `record_type`, `risk_level`, `environment`, `success` |

## How To Read The Signals

- Native execution should trend upward as more domain requests resolve to capabilities.
- Bridge fallback should remain zero except for explicit retired-legacy requests that return `LEGACY_RUNTIME_RETIRED`.
- `tilsoftai_runtime_adapter_failures_total` by `adapter=rest-json` or `adapter=sql` identifies integration boundaries causing failures.
- `CAPABILITY_ACCESS_DENIED` in adapter-failure labels means capability policy stopped execution before adapter resolution.
- `tilsoftai_runtime_capability_invocations_total` shows which domain capabilities are actually used.
- `tilsoftai_runtime_approval_executions_total` confirms writes are still governed by `IApprovalEngine` and adapter-level guards.
- Duration histograms split by `path` let operations compare native, approval, and supervisor latency. Bridge labels are historical/retired-path signals only.
- `tilsoftai_platform_catalog_source_mode_total{mode="bootstrap_only"}` or `{mode="mixed"}` means bootstrap fallback is still active and should be reviewed.
- `tilsoftai_platform_catalog_mutations_total{success="false"}` means a catalog governance, validation, or persistence operation failed.

## Structured Log Events

Runtime instrumentation emits `RuntimeExecutionObserved` for supervisor, native, retired bridge, and approval paths. Adapter failures emit `RuntimeAdapterFailureObserved`.

Catalog startup emits `PlatformCatalogSourceReport` with source mode, environment, production-like status, platform counts, bootstrap counts, and integrity status. When fallback is active outside production it emits `PlatformCatalogBootstrapFallbackActive`; in production-like environments it emits `PlatformCatalogBootstrapFallbackProductionRisk`.

Catalog mutation emits `PlatformCatalogMutationProposed` and governance/config-change audit events for submit, duplicate-submit replay, approve, reject, apply, and apply replay operations.

Look for these fields:
- `Path`: `supervisor`, `native`, `bridge`, or `approval`
- `AgentId`
- `CapabilityKey`
- `AdapterType`
- `DurationMs`
- `Success`
- `Reason` for bridge fallback
- `ErrorCode` for adapter failures

Retired bridge reasons are explicit:
- `explicit_legacy_fallback`: request used `legacy-chat` or `legacyFallback=true` and was rejected with `LEGACY_RUNTIME_RETIRED`.
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
- `LEGACY_RUNTIME_RETIRED`: explicit legacy fallback was requested after bridge/ChatPipeline retirement.

## Operational Triage

1. If user traffic succeeds but native counts are low, inspect capability resolution logs and unsupported general responses.
2. If native failure counts rise for one capability, inspect adapter failures for the same `capability` and `adapter` labels.
3. If approval execution failures rise, inspect write-action catalog, role validation, and `IWriteActionGuard` rejection logs.
4. If `LEGACY_RUNTIME_RETIRED` appears, map the request class to a native capability or a supported supervisor-native general workflow.

## Readiness

`/health/ready` includes `platform-catalog` and `native-runtime`, not `modules`. The catalog check reports platform/bootstrap source mode and integrity. The native check verifies supervisor runtime resolution, all loaded native capabilities, and registered adapters generically. Module health remains available as a legacy diagnostic check outside the ready tag. See `runtime_readiness.md`.
