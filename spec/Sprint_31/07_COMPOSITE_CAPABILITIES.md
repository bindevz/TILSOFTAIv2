# Phase 7 — Composite Capabilities for Multi-Proc Read Aggregation

## Goal

Support user questions that require multiple data sources or stored procedures, while avoiding uncontrolled model-driven tool chaining.

Prefer declarative composite capabilities over free-form multi-tool calls.

## Principle

The model should select a composite capability when the business intent is known.

The system, not the model, decides which sub-capabilities are executed.

## Example

User asks:

```text
Give me a 360 view of customer ABC.
```

Composite capability:

```json
{
  "capabilityKey": "sales.customer.360",
  "domain": "sales",
  "functionName": "sales_customer_360",
  "executionMode": "composite",
  "subCapabilities": [
    "sales.orders.by-customer",
    "accounting.receivables.by-customer",
    "warehouse.pending-shipments.by-customer"
  ],
  "compositionPolicy": {
    "mode": "parallel",
    "joinKey": "CustomerCode",
    "output": "json_bundle"
  }
}
```

## Add executor

Create:

```text
src/TILSOFTAI.Orchestration/Execution/
  CompositeCapabilityExecutor.cs
```

Interface:

```csharp
public interface ICompositeCapabilityExecutor
{
    Task<CapabilityExecutionEnvelope> ExecuteAsync(
        string compositeCapabilityKey,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken);
}
```

## Execution flow

```text
CompositeCapabilityExecutor
  -> load composite capability
  -> validate shared arguments
  -> load sub-capabilities
  -> verify all are read-only unless explicitly allowed
  -> execute sub-capabilities through CapabilityExecutionFacade
  -> merge result envelopes
  -> return merged envelope to AnswerComposer
```

## Composition policies

Support at least:

```text
parallel
sequential
json_bundle
simple_join_by_key
```

Start with `parallel + json_bundle` for fastest delivery.

## Multi-tool policy

Default:

```json
{
  "allowModelSelectedMultiTool": false,
  "preferCompositeCapability": true,
  "maxToolCallsPerTurn": 3
}
```

Phase 7 should not allow open-ended model-controlled multi-tool execution unless explicitly enabled.

## Answer composer integration

Composite result should include:

- parent capability key
- sub-capability envelopes
- row counts per sub-capability
- merged rows or JSON bundle
- execution metadata per call
- overall correlation ID

Raw JSON mode should return the full bundle after sensitivity masking.

Structured mode should summarize each section and optionally include tables per section.

## Acceptance criteria

- A composite capability can execute multiple read-only sub-capabilities.
- Sub-capability execution still goes through `CapabilityExecutionFacade`.
- Unauthorized sub-capabilities are blocked.
- Results can be returned as raw JSON bundle.
- Structured answer can summarize the composite result.
- The model does not freely choose arbitrary multi-tool chains in this phase.
