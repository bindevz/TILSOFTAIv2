# SQL Compatibility Observability Runbook

The remaining legacy SQL compatibility shell is measurable and governed. The legacy physical storage names still exist for deployed database safety, but operators can see whether callers are using old procedure names or the forward capability-scope wrappers, and raw telemetry now has a rollup/purge lifecycle.

## Signals

| Signal | Source | Meaning |
|--------|--------|---------|
| Legacy procedure usage | `dbo.SqlCompatibilityUsageLog` where `SurfaceKind = 'legacy-procedure'` | A caller still depends on old SQL procedure names or legacy parameter names. |
| Capability-scope wrapper usage | `dbo.SqlCompatibilityUsageLog` where `SurfaceKind = 'capability-scope-wrapper'` | A caller is using the forward-facing SQL procedure names. |
| Daily usage trend | `dbo.SqlCompatibilityUsageDaily` | Per-day usage grouped by surface, tenant, app, and language. |
| Retained daily rollup | `dbo.SqlCompatibilityUsageRollup` | Long-lived summarized evidence retained after raw rows are purged. |
| Summary by surface | `dbo.app_sql_compatibility_usage_summary` | Operator-friendly query for current usage counts and last-seen timestamps. |
| DB-major readiness snapshot | `dbo.app_sql_compatibility_retirement_readiness` | A compact go/no-go view for physical rename planning. |

## Procedures That Record Usage

Legacy compatibility procedures record `legacy-procedure`:

- `dbo.app_modulecatalog_list`
- `dbo.app_toolcatalog_list_scoped`
- `dbo.app_metadatadictionary_list_scoped`
- `dbo.app_policy_resolve`
- `dbo.app_react_followup_list_scoped`
Optional legacy diagnostics record `legacy-procedure` only when explicitly deployed:

- `dbo.app_module_runtime_list`

Forward compatibility wrappers record `capability-scope-wrapper`:

- `dbo.app_capabilityscope_list`
- `dbo.app_toolcatalog_list_by_capability_scope`
- `dbo.app_metadatadictionary_list_by_capability_scope`
- `dbo.app_policy_resolve_by_capability_scope`
- `dbo.app_react_followup_list_by_capability_scope`

Usage recording is non-blocking. If the telemetry procedure is unavailable or fails, the compatibility path continues.

## Lifecycle Policy

Raw compatibility usage rows are operational telemetry, not permanent release evidence. The default policy is:

| Data | Default lifecycle |
|------|-------------------|
| Raw rows in `SqlCompatibilityUsageLog` | Retain 90 days by default; minimum allowed purge retention is 30 days. |
| Daily rollups in `SqlCompatibilityUsageRollup` | Retain as release evidence until superseded by the organization's audit-retention policy. |
| Release readiness packet | Attach to the release record for DB-major rename decisions. |

Roll up and purge old raw rows:

```sql
EXEC dbo.app_sql_compatibility_usage_purge
    @RawRetentionDays = 90;
```

Roll up without deleting raw rows:

```sql
EXEC dbo.app_sql_compatibility_usage_rollup
    @ThroughUtc = DATEADD(day, -90, SYSUTCDATETIME());
```

The summary and readiness procedures read `SqlCompatibilityUsageDaily`, which combines retained rollups and current raw rows.

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

## Release Evidence Packet

For a DB-major decision, attach a completed packet based on `docs/db_major_readiness_evidence_packet.template.json`. The packet should include:

- compatibility inventory version and hash from `docs/compatibility_inventory.json`
- telemetry window start/end
- usage summary output
- readiness output
- rollback posture
- operator communication reference
- validation results
