# Deep Analytics Validation Boundary

Sprint 9 classifies the deep analytics E2E path as an external deep workflow validation suite.

The suite owner is Analytics. It validates catalog-to-plan-to-execute-to-render behavior against a real SQL Server analytics catalog, so it is intentionally not part of the default deterministic integration baseline.

## Execution Rule

Run the deep workflow suite only when `TEST_SQL_CONNECTION` points to a seeded analytics test database.

```powershell
dotnet test tests\TILSOFTAI.IntegrationTests\TILSOFTAI.IntegrationTests.csproj --filter "Category=ExternalDeepWorkflow"
```

When `TEST_SQL_CONNECTION` is not set, the guarded test reports a bounded skip with the reason `External SQL validation boundary not enabled`.

## Default Baseline

The default unit and integration baselines remain responsible for native routing, capability policy, argument contracts, REST governance, auth-enabled paths, and deterministic analytics components that do not require external SQL state.
