# Phase 3 — Dynamic Function Tools and Multilingual Descriptions

## Goal

Build model-facing function tools dynamically from SQL Server 2025 semantic metadata instead of hardcoding descriptions and aliases in C#.

The system is multilingual. Tool and parameter descriptions must be localized and generated at runtime.

## Key rule

Do not create a generic production tool like:

```text
execute_sql(proc_name, json)
execute_capability(capability_key, arguments_json)
```

Instead, create semantic, specific model-facing functions:

```text
warehouse_inventory_by_item(item_no, warehouse_code)
purchasing_open_purchase_orders_by_supplier(supplier_code, status, from_date, to_date)
sales_orders_by_customer(customer_code, from_date, to_date)
accounting_receivables_by_customer(customer_code, as_of_date)
```

Internally, all functions may call a generic executor:

```csharp
await _capabilityExecutionFacade.ExecuteReadAsync(capabilityKey, arguments, cancellationToken);
```

## Add tool factory

Create:

```text
src/TILSOFTAI.Orchestration/AiRouting/Tools/
  IAgentFunctionToolFactory.cs
  DynamicFunctionToolFactory.cs
  CapabilityFunctionNameMapper.cs
  CapabilityToolDescriptionBuilder.cs
  CapabilityParameterSchemaBuilder.cs
```

Interface:

```csharp
public interface IAgentFunctionToolFactory
{
    Task<IReadOnlyList<object>> BuildToolsAsync(
        IReadOnlyList<CapabilityCandidate> candidates,
        TilsoftExecutionContext context,
        string locale,
        CancellationToken cancellationToken);
}
```

Use the exact Microsoft Agent Framework function-tool type in the implementation. Keep the public interface adaptable if provider-specific types are difficult to abstract.

## Metadata sources

For each candidate capability, load:

- `ai.Capability.FunctionName`
- `ai.Capability.ExecutionMode`
- `ai.CapabilityText` for requested locale
- fallback `ai.CapabilityText` for default locale
- `ai.CapabilityArgument`
- `ai.ArgumentText` for requested locale
- argument aliases and examples
- result schema summary
- answer policy summary
- sensitivity policy summary
- top related examples from `ai.KnowledgeChunk`

## Description template

Generate a function description like this:

```text
Business domain: {domain}
Purpose: {localized_description}
Use when: {use_when}
Do not use when: {do_not_use_when}
Input requirements:
- {argument_name}: {argument_description}. Aliases: {top_aliases}. Examples: {top_examples}.
Execution mode: {execution_mode}
Safety: Never invent IDs. Ask clarification if required inputs are missing or ambiguous.
```

Keep descriptions concise. The agent receives only candidate tools, but each description still affects tool selection quality.

## Parameter schema strategy

Map `ai.CapabilityArgument` to model-facing parameter schema:

```text
ArgumentName        -> model-facing name, e.g. item_no
ProcParameterName   -> SQL-facing name, e.g. @ItemNo
DataType            -> string, integer, decimal, boolean, date, datetime, enum, object, array
IsRequired          -> required schema property
ValidationRule      -> allowed values, regex, min/max, format
ClarificationPolicy -> question to ask when missing/invalid
```

Parameter descriptions must come from `ai.ArgumentText`.

## Function callback behavior

Each generated tool callback must:

1. Receive model-facing arguments.
2. Attach metadata: capability key, function name, locale, correlation ID.
3. Call `CapabilityExecutionFacade`.
4. Return a `CapabilityExecutionEnvelope` or equivalent safe result.

Pseudo-code:

```csharp
private async Task<CapabilityExecutionEnvelope> InvokeCapabilityAsync(
    string capabilityKey,
    IReadOnlyDictionary<string, object?> modelFacingArguments,
    TilsoftExecutionContext context,
    CancellationToken cancellationToken)
{
    var capability = await _capabilityRepository.GetAsync(capabilityKey, cancellationToken);

    return capability.ExecutionMode switch
    {
        "read_only" => await _facade.ExecuteReadAsync(
            capabilityKey,
            modelFacingArguments,
            cancellationToken),

        "write_preview" => await _facade.PreviewWriteAsync(
            capabilityKey,
            modelFacingArguments,
            cancellationToken),

        "composite" => await _compositeExecutor.ExecuteAsync(
            capabilityKey,
            modelFacingArguments,
            cancellationToken),

        _ => CapabilityExecutionEnvelope.Blocked(
            capabilityKey,
            $"Unsupported execution mode: {capability.ExecutionMode}")
    };
}
```

## Naming conventions

Use clear function names:

```text
{domain}_{business_action}_{business_object}_{qualifier}
```

Examples:

```text
warehouse_inventory_by_item
warehouse_stock_movement_by_item
purchasing_open_purchase_orders_by_supplier
sales_orders_by_customer
accounting_receivables_by_customer
product_model_by_code
```

Avoid names like:

```text
run_proc
query_data
get_data
execute_tool
model_query
```

## Description hardcoding rule

Allowed in POC only:

```csharp
[Description("Temporary POC description")]
```

Not allowed in production:

```csharp
[Description("Permanent ERP tool description")]
```

Production descriptions must come from SQL KB.

## Domain gating

The factory must receive candidates from the retriever. It must not query and build all active capabilities.

Hard limits:

```text
maxDomainsPerRequest <= 2
maxToolsPerDomain <= 6
maxTotalTools <= 12
```

## Acceptance criteria

- Function names are generated from capability metadata.
- Function descriptions are loaded from SQL KB.
- Parameter descriptions are loaded from SQL KB.
- The model sees specific semantic functions, not generic executor functions.
- The tool factory builds only candidate tools.
- Tool callbacks never call SQL directly.
- Tool callbacks invoke `CapabilityExecutionFacade`.
