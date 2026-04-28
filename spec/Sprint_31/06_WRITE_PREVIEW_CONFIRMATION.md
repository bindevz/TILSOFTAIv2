# Phase 6 — Write Preview, User Confirmation, and Approval

## Goal

Allow users to create/update/delete ERP data through chat, but only through a safe two-step flow:

```text
preview -> user confirmation -> approval -> execute
```

The agent must never execute write operations directly from a single natural-language request.

## Model-facing tools

Expose preview tools only:

```text
sales_order_create_preview
purchasing_po_create_preview
warehouse_receipt_create_preview
product_model_create_preview
```

Do not expose execution tools:

```text
sales_order_create_execute
purchasing_po_create_execute
warehouse_receipt_create_execute
```

## Flow

```text
User asks to create/update/delete data
  -> agent calls *_preview function
  -> CapabilityExecutionFacade.PreviewWriteAsync
  -> validate user permissions and business rules
  -> AnswerComposer returns ConfirmationBlock
  -> user confirms
  -> existing IApprovalEngine creates/approves action
  -> ExecuteApprovedWriteAsync with approvedActionId
  -> SqlToolAdapter executes write proc
  -> AnswerComposer returns success/failure
```

## Preview behavior

`PreviewWriteAsync` must:

- Load the capability.
- Verify it is a write preview capability.
- Check tenant/user/role access.
- Resolve entities.
- Validate arguments.
- Run business-rule validation.
- Return a draft action payload.
- Not execute the write stored procedure.

Preview result should be returned as a confirmation block:

```csharp
public sealed record ConfirmationBlock(
    string Title,
    string Summary,
    IReadOnlyDictionary<string, object?> DraftAction) : AnswerBlock("confirmation");
```

## User confirmation

The system must treat user confirmation as an explicit action.

Examples of valid confirmation intent:

```text
Confirm
Yes, create it
Xác nhận
Tạo đi
Đồng ý
```

Do not execute if confirmation is ambiguous.

## Approval engine

Reuse existing approval infrastructure:

- `IApprovalEngine`
- `IActionRequestStore`
- `IWriteActionGuard`
- existing `SqlToolAdapter` requirement for `approvedActionId`

`SqlToolAdapter` must still reject write execution if `approvedActionId` is missing.

## Execution behavior

`ExecuteApprovedWriteAsync` must:

1. Load the approved draft/action.
2. Verify the action belongs to the current tenant/user/session or authorized approver.
3. Verify the approval has not expired.
4. Verify the same capability and arguments are being executed.
5. Pass `approvedActionId` to the adapter.
6. Audit the final result.

## Agent Framework tool approval

If the selected Microsoft Agent Framework provider supports tool approval or human-in-the-loop hooks, it may be used as an additional UX/runtime feature.

However, the existing ERP `IApprovalEngine` remains the source of truth for write authorization.

## Capability catalog requirements

Write capabilities should be represented separately:

```json
{
  "capabilityKey": "purchasing.po.create-preview",
  "domain": "purchasing",
  "functionName": "purchasing_po_create_preview",
  "executionMode": "write_preview"
}
```

Actual execution capability:

```json
{
  "capabilityKey": "purchasing.po.create-execute",
  "domain": "purchasing",
  "functionName": "purchasing_po_create_execute",
  "executionMode": "write_execute"
}
```

Only `write_preview` function tools are exposed to the model.

## Acceptance criteria

- A user cannot create/update/delete ERP data in one step.
- The agent can prepare a write preview.
- The answer composer returns a confirmation block.
- Actual write execution requires explicit user confirmation.
- Actual write execution requires `approvedActionId`.
- `SqlToolAdapter` still blocks writes without approval.
- All write preview and execution events are audited.
