# Write-Path Governance Audit

As part of the Sprint 3 V3 Migration, a comprehensive audit was conducted to identify and secure all execution paths that can mutate external system state (primarily SQL stored procedures for the legacy tenant DB architecture).

## Governance Policy
- **NO direct domain tool writes**: All state-changing actions related to business logic must route through `IApprovalEngine`.
- **Adapter-Level Guard**: All underlying infrastructure execution layers (like `SqlToolAdapter`) require an explicit verification pass to ensure the execution corresponds to a validated, authorized `ActionId`.
- **Infrastructure Exemption**: Core platform stores (vector definitions, audit logs, conversation histories) are explicitly exempted if they do not mutate tenant business data.

---

## Inspected Paths & Current Status

### 1. Governed Execution Paths (Compliant)

| Component | Nature of Write | Governance Verification | Status |
| --- | --- | --- | --- |
| `ActionsController.Execute()` | API Execution | Explicitly resolves and executes an action by `actionId` via `IApprovalEngine.ExecuteAsync()`. | ✅ Fully Compliant |
| `ActionRequestWriteToolHandler` | Domain Mutation | Detects write intent via language model tool call; delegates payload generation to `IApprovalEngine.CreateAsync()`. Validated but deferred. | ✅ Fully Compliant |
| `SqlToolAdapter` | Adapter Dispatcher | Added `IWriteActionGuard` dependency. Will reject `ExecuteWriteAction` operation unless a matching `approvedActionId` is found in the task configuration. | ✅ Hardened in Sprint 3 |

### 2. Guarded Write Operations (Risk Mitigated)

| Component | Nature of Write | Mitigation | Status |
| --- | --- | --- | --- |
| `SqlExecutor.ExecuteWriteActionAsync()` | Raw SQL execution | Validates that target stored procedure exists and is `IsEnabled = 1` inside `WriteActionCatalog`. Can only be publicly reached via `SqlToolAdapter` (which is now guarded by `IWriteActionGuard`). | ⚠️ Moderately secure. Avoid injecting `ISqlExecutor` directly in application logic. |

### 3. Accepted Infrastructure Exceptions (Compliant)

The following components retain an unchecked write capability (e.g. bypassing the approval engine). These bypasses are intentional because these actions map to internal architectural state, rather than domain mutations.

| Component | Nature of Write | Context | Status |
| --- | --- | --- | --- |
| `SqlConversationStore` | Chat History | Only retains user chat prompt and tool output execution records. | ✅ Exempt (Platform) |
| `SqlAuditSink` | Audit Telemetry | Appends action trace IDs telemetry logs. | ✅ Exempt (Platform) |
| `AnalyticsCache` / `AnalyticsPersistence` | Observability data | Aggregates and dumps local observability trace counts. | ✅ Exempt (Platform) |
| `SemanticCache` / `SqlVectorSemanticCache` | Embeddings | Caches vector dimensions for faster subsequent retrieval. | ✅ Exempt (Platform) |
| `SqlErrorLogWriter` | Unhandled Exceptions | Dumps stack traces into a platform diagnostic table. | ✅ Exempt (Platform) |

## Future Work (Sprint 4+)

- Eventually replace raw `SqlExecutor` injection inside components mapped to `Exempt (Platform)` with dedicated internal domain Repositories.
- Expand `IWriteActionGuard` to support memory-based caching of verified `approvedActionIds` to minimize redundant DB lookups during batch executions.
