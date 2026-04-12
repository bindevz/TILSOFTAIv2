# Runtime Readiness - Sprint 10

Readiness is split by runtime responsibility.

## Platform Catalog Readiness

`/health/ready` includes `platform-catalog`.

The catalog check validates:

- platform catalog file discovery,
- catalog integrity status,
- platform capability and external connection counts,
- bootstrap capability and external connection counts,
- whether bootstrap fallback is allowed,
- active source mode.

Source modes:

| Mode | Health | Meaning |
|------|--------|---------|
| `platform` | Healthy | Durable platform catalog records are the active source of truth. |
| `mixed` | Degraded | Platform records are active, but bootstrap fallback records are also present. |
| `bootstrap_only` | Degraded | No platform records are available and bootstrap fallback is serving records. |
| `empty` | Unhealthy | No platform or bootstrap records are available. |

Catalog integrity failures are unhealthy and include validation error codes in health data.

## Native Runtime Readiness

`/health/ready` includes `native-runtime`.

The native check is domain-agnostic. It validates:

- `ISupervisorRuntime` can resolve.
- At least one native capability is loaded.
- Every adapter type referenced by loaded capabilities is registered.
- Capability counts and adapter types are reported in health data.

It does not require warehouse/accounting-specific hardcoding.

## Legacy Diagnostics

`modules` health is tagged `legacy` and `diagnostic`, not `ready`.

`Modules:EnableLegacyAutoload` controls whether the old module loader hosted service starts. When disabled, module health reports that legacy autoload is off and native readiness should be used for platform readiness.

`ChatPipeline` and `LegacyChatPipelineBridge` are no longer part of readiness because they are deleted.

## External Readiness

External REST capability readiness is represented by:

- registered `rest-json` adapter,
- loaded external capability descriptors,
- platform catalog external connection records,
- bootstrap `ExternalConnections:Connections` entries only when fallback is enabled,
- secret provider availability at execution time.

The readiness check verifies adapter registration generically. Secret presence is validated during execution to avoid exposing secret-bearing checks in readiness output.
