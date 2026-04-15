# Module Substrate Migration

The module substrate is retired as an active runtime path. The last package shells are removed, and capability-scope SQL wrappers provide the forward-facing database boundary.

## Runtime State

- API startup does not register module autoload, module health, module activation providers, or module options.
- The Platform and Analytics package shell projects are deleted.
- Production ownership is Supervisor + Domain Agents + Platform Catalog + Tool Adapters.
- `ITilsoftModule` is deleted.

## SQL Compatibility

Some physical SQL storage objects retain historical names:

- `ModuleCatalog`
- `ToolCatalogScope.ModuleKey`
- `MetadataDictionaryScope.ModuleKey`
- `RuntimePolicy.ModuleKey`
- `ReActFollowUpRule.ModuleKey`
- `@ModuleKeysJson`

These names now mean capability scope filters. They are not runtime module ownership, activation, or package loading semantics. Runtime callers should use the capability-scope views and procedures documented in `sql_capability_scope_migration.md`.

## Migration Rule

New production work must add capability records, tool records, adapter bindings, platform catalog records, or domain-agent routing. It must not add package loader activation or new module ownership.

Future database cleanup may rename physical storage names after `SqlCompatibilityUsageLog` telemetry confirms all deployed callers use capability-scope procedure names for the agreed evidence window.

## Optional Legacy Diagnostics

`ModuleRuntimeCatalog` and `app_module_runtime_list` are no longer part of the default core SQL deployment path. They live under `sql/97_legacy_diagnostics` for upgraded databases that still need historical package-runtime diagnostics during compatibility retirement.

New deployments should not deploy this optional diagnostic file unless an operator explicitly needs to inspect legacy package-runtime rows. Any usage is recorded as `legacy-procedure` compatibility telemetry and blocks DB-major retirement until explained.
