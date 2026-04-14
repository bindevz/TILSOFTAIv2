# Module Substrate Migration - Sprint 21

Sprint 20 retired the module substrate as an active runtime path. Sprint 21 removes the last package shells and adds capability-scope SQL wrappers.

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

These names now mean capability scope filters. They are not runtime module ownership, activation, or package loading semantics. Runtime callers should use the Sprint 21 capability-scope views and procedures documented in `sql_capability_scope_migration.md`.

## Migration Rule

New production work must add capability records, tool records, adapter bindings, platform catalog records, or domain-agent routing. It must not add package loader activation or new module ownership.

Future database cleanup may rename physical storage names after telemetry confirms all deployed callers use capability-scope procedure names.
