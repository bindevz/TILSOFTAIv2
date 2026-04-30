# Sprint 44 — AI Answer Narrator, No Hardcoded Domain Summary

## Goal

Remove all domain-specific hardcoded summary logic from `StructuredAnswerComposer` and introduce a guarded AI-backed answer narration layer.

This sprint must make the `AnswerComposer` framework reusable for future domains without adding functions such as:

```text
BuildDeterministicPurchasingSummary
BuildDeterministicSalesSummary
BuildDeterministicWarehouseSummary
```

The active reference domain remains `model`, but `StructuredAnswerComposer` must not know or switch on `model.*` capability keys.

---

## Current Problem

`StructuredAnswerComposer` currently contains hardcoded model summary logic such as:

```text
IsModelCapability(...)
BuildDeterministicModelSummary(...)
ResolveModelCount(...)
ExtractComparedModelCodes(...)
TryGetModelCode(...)
switch capabilityKey:
  model.count
  model.overview.by-code
  model.pieces.by-code
  model.materials.by-code
  model.packaging.by-code
  model.compare
```

This violates the Agent Framework Core design because capability-specific summary behavior belongs in the SQL-backed catalog and narration policy, not in C# domain-specific branches.

---

## Target Architecture

```text
StructuredAnswerComposer
  -> validates answer state
  -> applies sensitivity policy
  -> builds deterministic blocks
  -> delegates narrative text to IAnswerNarrationService
  -> returns AssistantAnswer

IAnswerNarrationService
  -> receives sanitized rows, result schema, arguments, catalog text, answer policy, locale
  -> generates narrative summary text
  -> never calls SQL
  -> never calls tools
  -> returns structured narration result

GenericSchemaSummaryFallback
  -> safe fallback when AI narrator is disabled or invalid
  -> catalog/schema-driven, not domain-specific
```

---

## Non-Negotiable Rules

1. Remove hardcoded model summary logic from `StructuredAnswerComposer`.
2. Do not add hardcoded summaries for any domain.
3. Do not let AI decide table/chart/follow-up/no-data/provenance.
4. AI summary must receive sanitized rows only.
5. RawJson mode must not call the narrator.
6. No-data and follow-up answers must not call the narrator.
7. Invalid AI output must fall back safely.
8. Narrative summary must be based only on supplied data, schema, args, and catalog metadata.
9. Do not open new business domains in this sprint.
10. Do not enable real write execution.

---

## Phase 0 — Preflight and Inventory

Run:

```bash
dotnet build
dotnet test
```

Inventory hardcoded summary logic:

```bash
rg "BuildDeterministicModelSummary|IsModelCapability|ResolveModelCount|ExtractComparedModelCodes|TryGetModelCode" src tests
rg "model\.count|model\.overview|model\.pieces|model\.materials|model\.packaging|model\.compare" src/TILSOFTAI.Orchestration/Answering tests/TILSOFTAI.Tests/Answering
```

Expected before work:

```text
These searches may return current hardcoded logic.
```

Expected after work:

```text
No production Answering code contains model-specific summary branches.
```

---

## Phase 1 — Introduce Narration Contracts

Create:

```text
src/TILSOFTAI.Orchestration/Answering/Narration/IAnswerNarrationService.cs
src/TILSOFTAI.Orchestration/Answering/Narration/AnswerNarrationRequest.cs
src/TILSOFTAI.Orchestration/Answering/Narration/AnswerNarrationResult.cs
src/TILSOFTAI.Orchestration/Answering/Narration/AnswerNarrationPolicy.cs
```

Suggested contracts:

```csharp
public interface IAnswerNarrationService
{
    Task<AnswerNarrationResult> GenerateAsync(
        AnswerNarrationRequest request,
        CancellationToken cancellationToken);
}

public sealed record AnswerNarrationRequest
{
    public required string Locale { get; init; }
    public required string CapabilityKey { get; init; }
    public string? UserQuestion { get; init; }
    public string? CapabilityName { get; init; }
    public string? CapabilityDescription { get; init; }
    public required IReadOnlyDictionary<string, object?> Arguments { get; init; }
    public required ResultSchema? ResultSchema { get; init; }
    public required IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; init; }
    public required int RowCount { get; init; }
    public required AnswerPolicy AnswerPolicy { get; init; }
    public required SensitivityPolicy SensitivityPolicy { get; init; }
    public required IReadOnlyDictionary<string, object?> ExecutionMetadata { get; init; }
}

public sealed record AnswerNarrationResult
{
    public required string Text { get; init; }
    public double? Confidence { get; init; }
    public IReadOnlyList<string> UsedColumns { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public bool UsedFallback { get; init; }
}
```

Rules:

```text
- Keep these contracts domain-neutral.
- Do not include Model-specific fields.
- Do not pass raw SQL result payloads not already sanitized.
```

Acceptance:

```text
- Contracts compile.
- No model-specific wording appears in contract names or fields.
```

---

## Phase 2 — Add Generic Fallback Summary

Create:

```text
src/TILSOFTAI.Orchestration/Answering/Narration/GenericSchemaSummaryFallback.cs
```

Purpose:

```text
Return a safe summary when AI narration is disabled, unavailable, or invalid.
```

Fallback rules:

```text
- Use capability name/description from catalog if available.
- Use rowCount.
- Use visible result schema labels.
- Mention truncation if rows are trimmed.
- Do not infer business meaning beyond schema and rows.
- Do not switch on capability keys.
```

Example output:

```text
Found 8 rows for "Model materials". Showing the available structured result.
```

Vietnamese example:

```text
Tìm thấy 8 dòng cho "Nguyên liệu model". Dữ liệu được hiển thị trong bảng bên dưới.
```

The phrase `"Model materials"` must come from catalog metadata, not C# hardcode.

Acceptance:

```text
- Fallback works without LLM.
- Fallback does not reference model.* capability keys.
- Tests verify fallback is generic.
```

---

## Phase 3 — Add Agent-Based Narration Service

Create:

```text
src/TILSOFTAI.Orchestration/Answering/Narration/AgentAnswerNarrationService.cs
src/TILSOFTAI.Orchestration/Answering/Narration/AnswerNarrationPromptBuilder.cs
src/TILSOFTAI.Orchestration/Answering/Narration/AnswerNarrationResponseParser.cs
```

Use the configured AI provider. Prefer the existing `IChatClient`/official Agent Framework provider infrastructure.

Narrator rules:

```text
- No tools.
- No SQL.
- No function calling required.
- Input is sanitized rows + schema + args + policy.
- Output must be JSON only.
```

Required JSON output schema:

```json
{
  "text": "Short business summary based only on supplied data.",
  "confidence": 0.85,
  "usedColumns": ["ColumnA", "ColumnB"],
  "warnings": []
}
```

System instruction must include:

```text
You are an ERP data summarizer.
Use only supplied rows, schema, arguments, and metadata.
Do not invent fields, totals, causes, recommendations, or missing records.
Do not expose hidden or masked fields.
Do not mention stored procedure names.
If data is insufficient, say so.
Return JSON only.
```

Acceptance:

```text
- Valid JSON output is parsed into AnswerNarrationResult.
- Invalid JSON uses GenericSchemaSummaryFallback.
- Empty or unsafe text uses fallback.
- Narrator can be disabled by configuration.
```

---

## Phase 4 — Refactor StructuredAnswerComposer

Update:

```text
src/TILSOFTAI.Orchestration/Answering/StructuredAnswerComposer.cs
```

Remove production code:

```text
IsModelCapability
BuildDeterministicModelSummary
ResolveModelCount
ExtractComparedModelCodes
TryGetModelCode if used only by model summary
Any switch/if branch on model.* capability keys
```

New flow:

```text
1. If clarification required -> follow-up answer.
2. If error -> error answer.
3. If write preview -> write preview answer.
4. Sanitize rows.
5. If rowCount == 0 -> no-data answer.
6. Build table/chart blocks deterministically.
7. Build AnswerNarrationRequest.
8. Call IAnswerNarrationService.
9. Return AssistantAnswer with narrator text + deterministic blocks/provenance.
```

Rules:

```text
- StructuredAnswerComposer must be capability-agnostic.
- It may use AnswerPolicy and ResultSchema.
- It may not switch on capabilityKey for narrative text.
```

Acceptance:

```text
- Existing model structured responses still work.
- Summary text now comes from IAnswerNarrationService or fallback.
- Table/provenance remains deterministic.
```

---

## Phase 5 — Register Services

Update DI:

```text
OrchestrationServiceCollectionExtensions.cs
AddTilsoftAi* extension files if needed
```

Register:

```text
IAnswerNarrationService
AnswerNarrationPromptBuilder
AnswerNarrationResponseParser
GenericSchemaSummaryFallback
```

Configuration:

```json
{
  "Answering": {
    "Narration": {
      "Enabled": true,
      "UseAi": true,
      "FallbackOnInvalidOutput": true,
      "MaxRowsForNarration": 20,
      "MaxOutputCharacters": 1200
    }
  }
}
```

Rules:

```text
- RawJson composer must not resolve or call IAnswerNarrationService.
- If AI narration is disabled, use fallback.
- Test/local config may enable AI narration.
```

---

## Phase 6 — Update Tests

Remove or rewrite tests that assert exact hardcoded model text.

Delete expectations like:

```text
"There are 6 models in the current data."
"Model ABC has 8 materials."
"Compared 2 models: ABC and XYZ."
```

Add tests:

```text
StructuredAnswerComposer_UsesNarrationServiceText
StructuredAnswerComposer_DoesNotSwitchOnModelCapabilityKeys
StructuredAnswerComposer_SendsSanitizedRowsToNarrator
StructuredAnswerComposer_DoesNotCallNarratorForRawJson
StructuredAnswerComposer_DoesNotCallNarratorForNoData
StructuredAnswerComposer_DoesNotCallNarratorForFollowUp
AgentAnswerNarrationService_InvalidJson_UsesFallback
GenericSchemaSummaryFallback_IsCapabilityAgnostic
Architecture_NoHardcodedModelSummaryInAnswering
```

Use fake narrator in unit tests:

```csharp
public sealed class FakeAnswerNarrationService : IAnswerNarrationService
{
    public List<AnswerNarrationRequest> Requests { get; } = [];

    public Task<AnswerNarrationResult> GenerateAsync(
        AnswerNarrationRequest request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(new AnswerNarrationResult
        {
            Text = "AI-generated summary from fake narrator.",
            Confidence = 0.9
        });
    }
}
```

Acceptance:

```text
- Unit tests do not call real LLM.
- Tests prove narrator receives sanitized data.
- Tests prove AnswerComposer remains domain-neutral.
```

---

## Phase 7 — Architecture Guards

Add or update architecture tests:

```text
Architecture_NoBuildDeterministicModelSummary
Architecture_NoModelCapabilitySwitchInStructuredAnswerComposer
Architecture_RawJsonDoesNotUseNarration
Architecture_AnswerNarrationIsDomainNeutral
```

Guard checks:

```text
- StructuredAnswerComposer source must not contain BuildDeterministicModelSummary.
- StructuredAnswerComposer source must not contain model.count/model.overview/model.materials/model.compare.
- No production Answering type name contains ModelSummary.
```

Acceptance:

```text
- Hardcoded model summaries cannot return unnoticed.
```

---

## Final Validation

Run:

```bash
dotnet build
dotnet test
```

Run model E2E smoke if local AI and SQL are available:

```text
- Có bao nhiêu model?
- Cho tôi xem thông tin model ABC
- Model ABC gồm những piece nào?
- Show materials for model ABC
- So sánh model ABC và XYZ
```

Expected:

```text
- Tool routing still works.
- SQL execution still works.
- Structured summary text comes from narrator/fallback.
- RawJson unchanged.
```

## Definition of Done

```text
- BuildDeterministicModelSummary is removed.
- StructuredAnswerComposer has no model-specific summary branch.
- IAnswerNarrationService exists.
- AI narration receives sanitized rows only.
- Invalid AI output falls back safely.
- RawJson does not call narrator.
- No-data/follow-up do not call narrator.
- Tests cover hardcode regression.
- No new business domain is added.
```
