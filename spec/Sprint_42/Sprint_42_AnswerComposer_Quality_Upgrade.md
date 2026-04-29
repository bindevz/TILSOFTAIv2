# Sprint 42 — AnswerComposer Quality Upgrade

## Goal

Upgrade `AnswerComposer` from basic response assembly to an enterprise-grade response boundary.

The runtime already proves that model tools can execute. This sprint improves output quality, determinism, localization, result-schema usage, table rendering, no-data behavior, follow-up behavior, and provenance.

Do not add new business domains. Do not change the Agent Framework brain. Do not enable real write execution.

---

## Enterprise Response Principles

1. Agent Framework selects tools and binds arguments.
2. `CapabilityExecutionFacade` executes capabilities.
3. `AnswerComposer` owns final response shape.
4. RawJson is deterministic and machine-readable.
5. Structured is deterministic and user-readable.
6. Sensitive fields are masked before display or summary.
7. No-data and follow-up are first-class response types.
8. Result schema drives labels, formatting, and table blocks.

---

## Phase 0 — Preflight

Run:

```bash
dotnet build
dotnet test
```

Inventory:

```bash
rg "AnswerComposer|RawJsonAnswerComposer|StructuredAnswerComposer|AnswerBlock|AiSummaryService|ResultSchema|SensitivityPolicy" src tests
```

---

## Phase 1 — Normalize Answer Model

Review and refine:

```text
AnswerComposerRequest.cs
AssistantAnswer.cs
AnswerBlock.cs
RawJsonAnswerComposer.cs
StructuredAnswerComposer.cs
AnswerDataSanitizer.cs
```

Required `AssistantAnswer` shape:

```text
answerType
text
blocks
detail
followUpQuestions
provenance
correlationId
locale
```

Required answer types:

```text
raw_json
structured
follow_up
no_data
error
write_preview
```

Acceptance:

```text
- All composers return a stable answerType.
- All answers include correlation/provenance when available.
- Tests assert answerType, not only text.
```

---

## Phase 2 — Result Schema-Driven Labels

Use result schema metadata to render labels.

Expected result schema column fields:

```json
{
  "name": "ModelCode",
  "label": "Model Code",
  "labelVi": "Mã model",
  "type": "string",
  "role": "dimension",
  "visible": true,
  "format": null
}
```

Rules:

```text
- Table blocks use labelVi when locale is vi-VN.
- Table blocks use label/en label when locale is en-US.
- Raw DB column names are fallback only.
- Hidden columns are not rendered.
- Unknown columns are rendered only if policy allows.
```

Add tests:

```text
Structured_Table_UsesVietnameseLabels
Structured_Table_UsesEnglishLabels
Structured_Table_HidesInvisibleColumns
```

---

## Phase 3 — Deterministic Domain Summaries for Model

Implement deterministic summaries for the current model capabilities.

Capabilities:

```text
model.count
model.overview.by-code
model.pieces.by-code
model.materials.by-code
model.compare
model.packaging.by-code
```

Examples:

```text
model.count:
  "Có 6 model trong dữ liệu hiện tại."

model.overview.by-code:
  "Tìm thấy model ABC. Dữ liệu tổng quan gồm 1 dòng."

model.pieces.by-code:
  "Model ABC có 4 piece."

model.materials.by-code:
  "Model ABC có 8 material."

model.compare:
  "Đã so sánh 2 model: ABC và XYZ."
```

Rules:

```text
- Do not hallucinate business conclusions not present in rows.
- Do not fabricate totals not provided by rows.
- Use rowCount and known columns only.
- If data is insufficient, state that data is insufficient.
```

Acceptance:

```text
- Model summaries are useful but conservative.
- No fabricated insight appears in tests.
```

---

## Phase 4 — No-Data and Follow-Up Quality

No-data response must include filters used.

Example:

```text
Không tìm thấy dữ liệu cho model ABC.
Điều kiện đã dùng:
- Model code: ABC
```

Follow-up response must be specific:

```text
Bạn muốn xem thông tin cho model nào? Vui lòng cung cấp mã model.
```

Rules:

```text
- Missing required argument must not call SQL.
- Follow-up must name the missing business field.
- No-data must not ask for missing params that were already provided.
```

Tests:

```text
Structured_MissingModelCode_AsksSpecificQuestion
Structured_NoData_IncludesUsedFilter
Structured_NoData_DoesNotInventAlternatives
```

---

## Phase 5 — Table and Truncation Policy

Rules:

```text
- rows <= 20: render table block.
- rows > 20: render first 20 rows and set truncated=true.
- Include total rowCount.
- Suggest narrowing filters when truncated.
- Do not silently drop rows without metadata.
```

Block shape:

```json
{
  "type": "table",
  "title": "Materials",
  "columns": [],
  "rows": [],
  "rowCount": 28,
  "displayedRows": 20,
  "truncated": true
}
```

Acceptance:

```text
- Table blocks include rowCount and displayedRows.
- Truncated results have follow-up suggestion.
```

---

## Phase 6 — RawJson Enterprise Envelope

RawJson detail must include:

```text
mode
capabilityKey
functionName
procedureName
arguments
rowCount
rows
resultSchema
executionMetadata
sensitivityPolicyApplied
provenance
```

Rules:

```text
- RawJson does not call LLM.
- RawJson preserves enough data for frontend rendering.
- RawJson masks sensitive fields if policy requires.
```

Acceptance:

```text
- RawJson API contract is consistent across all model capabilities.
```

---

## Phase 7 — Optional AI Summary Guardrail

If an AI summary service remains, it must be optional and guarded.

Rules:

```text
- Disabled by default for RawJson.
- Disabled by default for deterministic model structured responses unless explicitly configured.
- Receives sanitized data only.
- Must not be responsible for table/chart/follow-up decision.
```

Acceptance:

```text
- AnswerComposer can run without AI summary service.
- Summary service cannot override safety/provenance.
```

---

## Phase 8 — Tests and Report Update

Add tests:

```text
AnswerComposer_RawJson_AllModelCapabilities
AnswerComposer_Structured_AllModelCapabilities
AnswerComposer_Localization_ViAndEn
AnswerComposer_NoData
AnswerComposer_FollowUp
AnswerComposer_Truncation
AnswerComposer_SensitiveFieldMasking
```

Update:

```text
docs/reports/model_e2e_runtime_test_report.md
docs/reports/model_e2e_framework_tuning_backlog.md
```

with AnswerComposer quality findings.

## Definition of Done

```text
- AnswerComposer has stable answer types.
- Result schema drives labels and tables.
- Model summaries are deterministic and conservative.
- No-data/follow-up are high quality.
- RawJson envelope is complete.
- Structured blocks are frontend-friendly.
- Tests cover all model capabilities.
- No new business domain is added.
- Auth hardening is skipped.
```
