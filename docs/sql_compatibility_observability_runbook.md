# SQL Compatibility Observability Runbook

Sprint 22 makes the remaining legacy SQL compatibility shell measurable. The legacy physical storage names still exist for deployed database safety, but operators can now see whether callers are using the old procedure names or the forward capability-scope wrappers.

## Signals

| Signal | Source | Meaning |
|--------|--------|---------|
| Legacy procedure usage | `dbo.SqlCompatibilityUsageLog` where `SurfaceKind = 'legacy-procedure'` | A caller still depends on old SQL procedure names or legacy parameter names. |
| Capability-scope wrapper usage | `dbo.SqlCompatibilityUsageLog` where `SurfaceKind = 'capability-scope-wrapper'` | A caller is using the forward-facing SQL procedure names. |
| Daily usage trend | `dbo.SqlCompatibilityUsageDaily` | Per-day usage grouped by surface, tenant, app, and language. |
| Summary by surface | `dbo.app_sql_compatibility_usage_summary` | Operator-friendly query for current usage counts and last-seen timestamps. |
| DB-major readiness snapshot | `dbo.app_sql_compatibility_retirement_readiness` | A compact go/no-go view for physical rename planning. |

## Procedures That Record Usage

Legacy compatibility procedures record `legacy-procedure`:

- `dbo.app_modulecatalog_list`
- `dbo.app_toolcatalog_list_scoped`
- `dbo.app_metadatadictionary_list_scoped`
- `dbo.app_policy_resolve`
- `dbo.app_react_followup_list_scoped`
- `dbo.app_module_runtime_list`

Forward compatibility wrappers record `capability-scope-wrapper`:

- `dbo.app_capabilityscope_list`
- `dbo.app_toolcatalog_list_by_capability_scope`
- `dbo.app_metadatadictionary_list_by_capability_scope`
- `dbo.app_policy_resolve_by_capability_scope`
- `dbo.app_react_followup_list_by_capability_scope`

Usage recording is non-blocking. If the telemetry procedure is unavailable or fails, the compatibility path continues.

## Operator Queries

Use the summary procedure for a rolling window:

```sql
EXEC dbo.app_sql_compatibility_usage_summary
    @SinceUtc = DATEADD(day, -30, SYSUTCDATETIME());
```

Check DB-major readiness:

```sql
EXEC dbo.app_sql_compatibility_retirement_readiness
    @SinceUtc = DATEADD(day, -30, SYSUTCDATETIME());
```

Investigate legacy callers by host or application:

```sql
SELECT
    SurfaceName,
    TenantId,
    AppKey,
    HostName,
    AppName,
    SessionLogin,
    COUNT_BIG(*) AS UsageCount,
    MAX(ObservedAtUtc) AS LastObservedAtUtc
FROM dbo.SqlCompatibilityUsageLog
WHERE SurfaceKind = N'legacy-procedure'
  AND ObservedAtUtc >= DATEADD(day, -30, SYSUTCDATETIME())
GROUP BY SurfaceName, TenantId, AppKey, HostName, AppName, SessionLogin
ORDER BY LastObservedAtUtc DESC;
```

## Interpretation

| Observation | Action |
|-------------|--------|
| Legacy usage is non-zero | Do not rename physical SQL storage names. Identify callers by host, app, login, tenant, and surface. |
| Legacy usage is zero and wrapper usage is non-zero | Candidate for DB-major planning after the required quiet window and deployment inventory checks pass. |
| Both usage counts are zero | Confirm the environment receives representative traffic before making a rename decision. |
| Wrapper usage drops unexpectedly | Investigate rollout, connection strings, and runtime deployment health before treating the data as readiness evidence. |

## Evidence Window

The default readiness procedure uses a 30-day window. Production environments should use the longer of:

- 30 calendar days after the final legacy binary is removed, or
- one complete release cycle with representative tenant traffic.

DB-major rename planning should not proceed from synthetic traffic alone.
