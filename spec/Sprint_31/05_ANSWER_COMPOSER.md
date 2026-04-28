# Phase 5 — Answer Composer

## Goal

Create a clear response composition layer with two main modes:

1. `RawJson`: return deterministic raw JSON without AI summarization.
2. `Structured`: receive capability/proc/arguments/result schema/rows/metadata/policy/locale and decide whether to return text, table, chart, summary, confirmation, or follow-up question.

## Add answer composer folder

Create:

```text
src/TILSOFTAI.Orchestration/Answering/
  IAnswerComposer.cs
  AnswerComposerRequest.cs
  AssistantAnswer.cs
  AnswerBlock.cs
  RawJsonAnswerComposer.cs
  StructuredAnswerComposer.cs
  AiSummaryService.cs
  ResultSchema.cs
  AnswerPolicy.cs
  SensitivityPolicy.cs
```

## Interface

```csharp
public interface IAnswerComposer
{
    Task<AssistantAnswer> ComposeAsync(
        AnswerComposerRequest request,
        CancellationToken cancellationToken);
}

public enum AnswerMode
{
    RawJson,
    Structured
}

public sealed record AnswerComposerRequest
{
    public required AnswerMode Mode { get; init; }
    public required string CapabilityKey { get; init; }
    public required string? ProcedureName { get; init; }
    public required IReadOnlyDictionary<string, object?> Arguments { get; init; }
    public required ResultSchema? ResultSchema { get; init; }
    public required IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; init; }
    public required int RowCount { get; init; }
    public required ExecutionMetadata ExecutionMetadata { get; init; }
    public required SensitivityPolicy SensitivityPolicy { get; init; }
    public required string Locale { get; init; }
    public required AnswerPolicy AnswerPolicy { get; init; }
    public string? ClarificationQuestion { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}
```

## Output model

```csharp
public sealed record AssistantAnswer
{
    public required string AnswerType { get; init; }
    public required string Text { get; init; }
    public required IReadOnlyList<AnswerBlock> Blocks { get; init; }
    public IReadOnlyList<string> FollowUpQuestions { get; init; } = [];
    public required AnswerProvenance Provenance { get; init; }
}

public abstract record AnswerBlock(string Type);

public sealed record TextBlock(string Content) : AnswerBlock("text");

public sealed record TableBlock(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<object?>> Rows,
    int TotalRows,
    bool Truncated) : AnswerBlock("table");

public sealed record ChartBlock(
    string ChartType,
    string CategoryField,
    string ValueField,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Data) : AnswerBlock("chart");

public sealed record FollowUpBlock(
    string Question,
    IReadOnlyList<string> Options) : AnswerBlock("follow_up");

public sealed record ConfirmationBlock(
    string Title,
    string Summary,
    IReadOnlyDictionary<string, object?> DraftAction) : AnswerBlock("confirmation");

public sealed record RawJsonBlock(object Data) : AnswerBlock("raw_json");
```

## Raw JSON mode

Rules:

- Do not call the LLM.
- Apply sensitivity policy before returning data.
- Return deterministic envelope containing:
  - capability key
  - arguments
  - row count
  - rows
  - result schema
  - execution metadata
  - provenance

Example:

```json
{
  "answerType": "raw_json",
  "text": "",
  "blocks": [
    {
      "type": "raw_json",
      "data": {
        "capabilityKey": "warehouse.inventory.by-item",
        "arguments": {
          "@ItemNo": "MADEIRA-BLK"
        },
        "rowCount": 2,
        "rows": []
      }
    }
  ],
  "provenance": {
    "capabilityKey": "warehouse.inventory.by-item",
    "rowCount": 2,
    "correlationId": "..."
  }
}
```

## Structured mode behavior

Decision rules:

```text
If clarification is required:
  return follow-up question.

If validation failed:
  return concise error + missing/invalid arguments.

If rowCount = 0:
  explain no data found and repeat filters used.

If rowCount <= AnswerPolicy.MaxRowsForChat:
  return summary + table.

If rowCount > AnswerPolicy.MaxRowsForChat:
  return summary + top rows + truncated flag + suggest filter/export.

If result schema has date/time + numeric measure:
  add line chart candidate.

If result schema has category + numeric measure:
  add bar chart candidate.

If execution mode is write_preview:
  return confirmation block.

If sensitivity policy masks columns:
  mask before display and before AI summary.
```

## Result schema

Result schema should include labels and semantic roles:

```json
{
  "columns": [
    {
      "name": "WarehouseName",
      "label": "Warehouse",
      "type": "string",
      "role": "dimension",
      "visible": true
    },
    {
      "name": "AvailableQty",
      "label": "Available Quantity",
      "type": "decimal",
      "role": "measure",
      "format": "number",
      "visible": true
    }
  ],
  "defaultSort": [
    {
      "column": "AvailableQty",
      "direction": "desc"
    }
  ],
  "chartHints": [
    {
      "type": "bar",
      "category": "WarehouseName",
      "value": "AvailableQty"
    }
  ]
}
```

## AI summary policy

AI summary is optional and only allowed after deterministic masking/filtering.

Allowed input to model:

- safe rows only
- capped row set
- result schema labels
- arguments used
- locale
- business meaning from result schema

Never pass:

- secrets
- hidden columns
- unmasked PII/financial fields if policy forbids
- raw stored procedure names unless debug mode is enabled
- full large result sets beyond policy cap

## Locale behavior

Format according to locale:

- dates
- numbers
- currency
- decimal separators
- table labels
- summary language
- clarification questions

Default locale should come from request/user/tenant, with fallback to `vi-VN` if current product defaults to Vietnamese.

## Acceptance criteria

- `RawJson` mode returns deterministic JSON and does not call LLM.
- `Structured` mode returns `TextBlock`, `TableBlock`, `ChartBlock`, `FollowUpBlock`, or `ConfirmationBlock` as appropriate.
- Row count zero is handled clearly.
- Large result sets are truncated according to policy.
- Sensitive fields are masked before display and before AI summary.
- Provenance includes capability key, row count, and correlation ID.
