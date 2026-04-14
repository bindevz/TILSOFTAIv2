# Module Substrate Migration - Sprint 20

Sprint 20 retires the module substrate as an active runtime path.

## Runtime State

- API startup does not register module autoload, module health, module activation providers, or module options.
- The API project does not reference Platform or Analytics package projects.
- Production ownership is Supervisor + Domain Agents + Platform Catalog + Tool Adapters.
- Remaining package projects are solution-local compatibility artifacts only.

## SQL Compatibility

Some SQL objects retain historical names:

- `ModuleCatalog`
- `ToolCatalogScope.ModuleKey`
- `MetadataDictionaryScope.ModuleKey`
- `RuntimePolicy.ModuleKey`
- `ReActFollowUpRule.ModuleKey`
- `@ModuleKeysJson`

These names now mean capability scope filters. They are not runtime module ownership, activation, or package loading semantics.

## Migration Rule

New production work must add capability records, tool records, adapter bindings, platform catalog records, or domain-agent routing. It must not add package loader activation or new module ownership.

Future database cleanup may rename `ModuleKey` and `@ModuleKeysJson` after all deployed databases and clients can tolerate the schema change.
