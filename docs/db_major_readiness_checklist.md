# DB-Major Readiness Checklist

This checklist defines when the remaining physical SQL compatibility names can be renamed or permanently abstracted. The current architecture is capability-scope oriented; the legacy table and column names remain only because deployed databases and rollback paths may still depend on them.

## Compatibility Surfaces

Physical storage names still retained for compatibility:

- `ModuleCatalog`
- `ToolCatalogScope.ModuleKey`
- `MetadataDictionaryScope.ModuleKey`
- `RuntimePolicy.ModuleKey`
- `ReActFollowUpRule.ModuleKey`

Legacy procedures still retained for older binaries:

- `app_modulecatalog_list`
- `app_toolcatalog_list_scoped`
- `app_metadatadictionary_list_scoped`
- `app_policy_resolve`
- `app_react_followup_list_scoped`

Optional legacy diagnostics, outside the default core deployment:

- `ModuleRuntimeCatalog`
- `app_module_runtime_list`

Forward-facing procedures current runtimes should use:

- `app_capabilityscope_list`
- `app_toolcatalog_list_by_capability_scope`
- `app_metadatadictionary_list_by_capability_scope`
- `app_policy_resolve_by_capability_scope`
- `app_react_followup_list_by_capability_scope`

## Go Criteria

All criteria must pass before scheduling a DB-major physical rename:

| Area | Required evidence |
|------|-------------------|
| Legacy usage | `app_sql_compatibility_retirement_readiness` reports zero legacy-procedure usage for the agreed production evidence window. |
| Forward usage | Capability-scope wrapper usage is present in representative environments during the same window. |
| Deployment inventory | All active API, worker, integration, reporting, and operator scripts are confirmed to call forward-facing procedure names. |
| Rollback stance | Rollback is a forward-compatible restore or rollback script, not a return to legacy procedure callers. |
| Test coverage | Unit, integration, SQL deployment, and representative smoke tests pass against the planned rename branch. |
| Operator communication | Operators have the affected surfaces, timing, rollback path, and post-cutover checks before the maintenance window. |
| Audit evidence | The readiness query output and deployment inventory are attached to the release record. |
| Evidence packet | A completed `docs/db_major_readiness_evidence_packet.template.json` packet is attached to the release record with inventory hash and telemetry output references. |

## No-Go Conditions

Do not schedule the DB-major rename if any of these are true:

- Any `legacy-procedure` usage appears in the readiness window.
- Wrapper usage is absent or not representative.
- Any active deployment still contains `@ModulesJson`, `@ModuleKeysJson`, `app_toolcatalog_list_scoped`, `app_metadatadictionary_list_scoped`, `app_policy_resolve`, or `app_react_followup_list_scoped` as runtime calls.
- Rollback requires reintroducing package loaders, package shell projects, or module-runtime ownership.
- Operators cannot identify which tenants or applications were covered by the evidence window.
- `ModuleRuntimeCatalog` is deployed as a normal core schema object instead of an explicitly optional legacy diagnostic.

## Migration Sequence

1. Freeze new SQL compatibility changes except bug fixes.
2. Capture 30 days or one full release cycle of compatibility usage telemetry.
3. Verify no legacy procedure usage in production-like and production environments.
4. Run `app_sql_compatibility_usage_purge` only after confirming rollups cover the evidence window.
5. Run static repository checks for legacy runtime procedure names and parameter names.
6. Attach `docs/compatibility_inventory.json` and a completed readiness evidence packet to the release record.
7. Prepare a DB-major branch that renames physical storage names or formalizes the wrapper boundary.
8. Run SQL deployment in a production-like environment with restored production-shaped data.
9. Execute application smoke tests, catalog mutation tests, policy resolution tests, ReAct follow-up tests, and rollback tests.
10. Communicate the maintenance window and post-cutover evidence checks.
11. Deploy during the approved window.
12. Monitor legacy usage, wrapper usage, runtime errors, and policy resolution for the agreed observation period.

## Rollback Expectations

Rollback must be rehearsed before production execution. Acceptable rollback options are:

- restore the pre-migration database snapshot and redeploy the known-good forward-caller application build, or
- run a tested compatibility rollback script that restores the previous physical names and wrappers.

Rollback must not depend on reintroducing module loaders, package shell projects, or legacy runtime ownership concepts.

## Post-Cutover Checks

After the DB-major migration:

- `app_sql_compatibility_usage_summary` should show no new legacy-procedure calls.
- Runtime SQL calls should continue through capability-scope procedure names.
- Platform catalog health and native runtime readiness should remain healthy.
- Policy resolution and ReAct follow-up queries should match pre-cutover smoke-test results.
- Any legacy call is treated as a release blocker until explained and remediated.
