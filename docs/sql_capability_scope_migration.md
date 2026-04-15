# SQL Capability Scope Migration

The repository uses forward-facing capability-scope SQL wrappers while preserving deployed database compatibility.

## Inventory

| Legacy surface | Forward-facing surface | Sprint 21 state |
|----------------|------------------------|-----------------|
| `ModuleCatalog` | `CapabilityScopeCatalog` view | View added over the legacy table. |
| `ToolCatalogScope.ModuleKey` | `ToolCatalogCapabilityScope.CapabilityScopeKey` view column | View added over the legacy table. |
| `MetadataDictionaryScope.ModuleKey` | `MetadataDictionaryCapabilityScope.CapabilityScopeKey` view column | View added over the legacy table. |
| `RuntimePolicy.ModuleKey` | `RuntimePolicyCapabilityScope.CapabilityScopeKey` view column | View added over the legacy table. |
| `ReActFollowUpRule.ModuleKey` | `ReActFollowUpRuleCapabilityScope.CapabilityScopeKey` view column | View added over the legacy table. |
| `app_modulecatalog_list` | `app_capabilityscope_list` | New procedure added. |
| `app_toolcatalog_list_scoped` + `@ModulesJson` | `app_toolcatalog_list_by_capability_scope` + `@CapabilityScopesJson` | Runtime callers moved to the new procedure. |
| `app_metadatadictionary_list_scoped` + `@ModulesJson` | `app_metadatadictionary_list_by_capability_scope` + `@CapabilityScopesJson` | Runtime callers moved to the new procedure. |
| `app_policy_resolve` + `@ModuleKeysJson` | `app_policy_resolve_by_capability_scope` + `@CapabilityScopesJson` | Runtime callers moved to the new procedure. |
| `app_react_followup_list_scoped` + `@ModuleKeysJson` | `app_react_followup_list_by_capability_scope` + `@CapabilityScopesJson` | Runtime callers moved to the new procedure. |

## Rollout Sequence

1. Deploy the capability-scope SQL wrappers and views.
2. Deploy application code that calls the capability-scope procedures.
3. Keep legacy procedures and tables in place for older binaries, existing deployments, and rollback.
4. Monitor legacy procedure and capability-scope wrapper usage through `SqlCompatibilityUsageLog`, `SqlCompatibilityUsageDaily`, and `app_sql_compatibility_usage_summary`.
5. After all deployed callers use capability-scope procedures, schedule a DB-major migration to rename physical columns/tables or keep views as the permanent abstraction.

## Observability

Sprint 22 adds a concrete compatibility-usage signal:

- legacy procedures record `legacy-procedure`
- capability-scope wrappers record `capability-scope-wrapper`
- `dbo.app_sql_compatibility_retirement_readiness` summarizes whether a DB-major rename is a candidate for the selected evidence window

See `docs/sql_compatibility_observability_runbook.md` for operator queries and `docs/db_major_readiness_checklist.md` for go/no-go criteria.

## Repository Layout

SQL deployment scripts for model and analytics capability objects live under `sql/02_capabilities`. The old `sql/02_modules` folder and module template folder were retired so new SQL work starts from capability-oriented structure.

## Rollback

Rollback is safe because Sprint 21 is additive at the SQL boundary. Older binaries can keep calling the legacy procedures and legacy parameter names. New binaries call wrappers that read the same underlying tables.

## Temporary Compatibility

The legacy physical table and column names remain temporarily supported:

- `ModuleCatalog`
- `ToolCatalogScope.ModuleKey`
- `MetadataDictionaryScope.ModuleKey`
- `RuntimePolicy.ModuleKey`
- `ReActFollowUpRule.ModuleKey`

These names must be treated as storage compatibility only. New application code and docs should use capability-scope procedure/view names.
