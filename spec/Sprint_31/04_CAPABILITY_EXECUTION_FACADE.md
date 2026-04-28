# Phase 4 — CapabilityExecutionFacade

## Goal

Create a single safe execution boundary between model-facing tools and ERP adapters.

All function tools must call `CapabilityExecutionFacade`. No function tool may call `SqlToolAdapter`, `SqlExecutor`, stored procedures, views, or ERP APIs directly.

## Add facade

Create:

```text
src/TILSOFTAI.Orchestration/Execution/
  ICapabilityExecutionFacade.cs
  CapabilityExecutionFacade.cs
  CapabilityExecutionEnvelope.cs
  CapabilityArgumentMapper.cs
  CapabilityExecutionPolicy.cs
```

Interface:

```csharp
public interface ICapabilityExecutionFacade
{
    Task<CapabilityExecutionEnvelope> ExecuteReadAsync(
        string capabilityKey,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken);

    Task<CapabilityExecutionEnvelope> PreviewWriteAsync(
        string capabilityKey,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken);

    Task<CapabilityExecutionEnvelope> ExecuteApprovedWriteAsync(
        string capabilityKey,
        string approvedActionId,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken);
}
```

## Envelope

```csharp
public sealed record CapabilityExecutionEnvelope
{
    public required string CapabilityKey { get; init; }
    public required string ExecutionMode { get; init; }
    public string? ProcedureName { get; init; }
    public required IReadOnlyDictionary<string, object?> Arguments { get; init; }
    public ResultSchema? ResultSchema { get; init; }
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; init; } = [];
    public int RowCount { get; init; }
    public required ExecutionMetadata ExecutionMetadata { get; init; }
    public required SensitivityPolicy SensitivityPolicy { get; init; }
    public required AnswerPolicy AnswerPolicy { get; init; }
    public string? ClarificationQuestion { get; init; }
    public IReadOnlyList<string> MissingArguments { get; init; } = [];
    public IReadOnlyList<string> InvalidArguments { get; init; } = [];
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public bool Success { get; init; }
}
```

Adapt types to match existing project models.

## Responsibilities

The facade must perform these steps:

```text
1. Load CapabilityDescriptor by capabilityKey.
2. Check active/version/certification state.
3. Evaluate tenant/user/role access.
4. Resolve entity aliases.
5. Normalize locale-specific values.
6. Map model-facing argument names to proc parameter names.
7. Validate argument contract using existing validator.
8. Enforce execution mode.
9. Enforce write preview/approval rules.
10. Call IToolAdapterRegistry and execute selected adapter.
11. Build CapabilityExecutionEnvelope.
12. Emit audit/telemetry.
```

## Read execution flow

```csharp
public async Task<CapabilityExecutionEnvelope> ExecuteReadAsync(
    string capabilityKey,
    IReadOnlyDictionary<string, object?> arguments,
    CancellationToken cancellationToken)
{
    var capability = await _capabilityRegistry.GetRequiredAsync(capabilityKey, cancellationToken);
    var context = _executionContextAccessor.GetRequired();

    var access = _accessPolicy.Evaluate(capability, context);
    if (!access.Allowed)
    {
        return CapabilityExecutionEnvelopeFactory.AccessDenied(capability, access);
    }

    if (!string.Equals(capability.ExecutionMode, "read_only", StringComparison.OrdinalIgnoreCase))
    {
        return CapabilityExecutionEnvelopeFactory.Blocked(
            capability,
            "Capability is not read-only. Use preview/approval flow.");
    }

    var resolvedArgs = await _entityAliasResolver.ResolveAsync(
        capability,
        arguments,
        context,
        cancellationToken);

    var procArgs = _argumentMapper.MapModelArgsToProcArgs(capability, resolvedArgs);

    var validation = _argumentValidator.Validate(capability, procArgs);
    if (!validation.IsValid)
    {
        return CapabilityExecutionEnvelopeFactory.ValidationFailed(
            capability,
            procArgs,
            validation);
    }

    var adapter = _toolAdapterRegistry.Resolve(capability.AdapterType);

    var request = ToolExecutionRequestFactory.Create(
        capability,
        procArgs,
        context);

    var result = await adapter.ExecuteAsync(request, cancellationToken);

    return CapabilityExecutionEnvelopeFactory.FromToolResult(
        capability,
        procArgs,
        result,
        context);
}
```

## Argument mapping

Model-facing arguments:

```json
{
  "item_no": "MADEIRA-BLK",
  "warehouse_code": "BD"
}
```

SQL-facing arguments:

```json
{
  "@ItemNo": "MADEIRA-BLK",
  "@WarehouseCode": "BD"
}
```

Mapping source:

```text
ai.CapabilityArgument.ArgumentName
ai.CapabilityArgument.ProcParameterName
```

## Entity alias resolution

Resolve only when the capability contract declares the argument as an entity or when the argument text indicates an entity type.

Examples:

```text
"BD" -> warehouse code / warehouse id
"ABC" -> customer or supplier candidate
"MADEIRA-BLK" -> item/product candidate
```

If multiple candidates are plausible, do not guess. Return a clarification envelope.

## Validation failures

Validation failure must not call SQL.

Return an envelope with:

```text
Success = false
MissingArguments
InvalidArguments
ClarificationQuestion
ErrorCode = "ARGUMENT_VALIDATION_FAILED"
```

## Adapter execution

Preferred SQL exposure:

```text
AI capability -> stored procedure wrapper -> ERP view/table/domain logic
```

Do not let the AI call views directly. Use wrapper stored procedures for:

- tenant filtering
- row limits
- stable result schema
- auditability
- parameter normalization
- security checks

## Acceptance criteria

- No function tool calls SQL or adapters directly.
- All execution goes through `CapabilityExecutionFacade`.
- Missing required arguments return clarification, not SQL errors.
- Unauthorized capabilities are blocked.
- Arguments are mapped from model-facing names to proc parameter names.
- Existing `CapabilityArgumentValidator` is used.
- Existing adapter registry is used.
- Read-only capabilities execute successfully through the facade.
