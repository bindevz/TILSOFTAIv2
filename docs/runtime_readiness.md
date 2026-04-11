# Runtime Readiness - Sprint 8

Readiness is split by runtime responsibility.

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

## External Readiness

External REST capability readiness is represented by:

- registered `rest-json` adapter,
- loaded external capability descriptors,
- configured `ExternalConnections:Connections` entries,
- secret provider availability at execution time.

The readiness check verifies adapter registration generically. Secret presence is validated during execution to avoid exposing secret-bearing checks in readiness output.
