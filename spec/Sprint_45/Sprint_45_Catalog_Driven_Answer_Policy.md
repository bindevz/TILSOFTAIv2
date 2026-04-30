# Sprint 45 — Catalog-Driven Answer Policy

## Goal

Move AnswerComposer behavior from C# defaults to SQL-backed catalog policy.

After Sprint 44, narrative summary is delegated to `IAnswerNarrationService`. This sprint ensures the behavior of the narrator and AnswerComposer is driven by SQL catalog metadata, not hardcoded C# decisions.

## Current Problem

The framework now has SQL-backed capabilities, but answer behavior may still be distributed across C# defaults and ad-hoc policy handling.

Enterprise-grade behavior should be governed by SQL catalog metadata:

```text
CapabilityAnswerPolicy
CapabilityText
CapabilityResultSchema
CapabilitySensitivityPolicy
```

---

## Target

For each capability, SQL catalog should drive:

```text
- summary mode
- summary instructions
- max summary rows
- max summary sentences
- table display limits
- no-data behavior
- follow-up behavior
- truncation notices
- sensitivity masking
- locale-specific wording hints
```

---

## Non-Negotiable Rules

1. Do not hardcode capability-specific answer behavior in C#.
2. SQL-backed catalog is the source of truth for answer policy.
3. AnswerComposer may have generic defaults only when policy is missing.
4. Missing invalid policy must be detected by catalog validation.
5. Do not add new business domains.
6. Do not enable real write execution.
7. Do not expose sensitive fields to narrator.

---

## Phase 0 — Preflight

Run:

```bash
dotnet build
dotnet test
```

Inventory answer policy usage:

```bash
rg "AnswerPolicy|CapabilityAnswerPolicy|allowAiSummary|maxRowsForChat|maxRowsForAiSummary|SummaryPolicy|NoData|FollowUp" src sql tests
```

---

## Phase 1 — Define Answer Policy Model

Create or refine:

```text
src/TILSOFTAI.Orchestration/Answering/Policies/AnswerPolicy.cs
src/TILSOFTAI.Orchestration/Answering/Policies/SummaryPolicy.cs
src/TILSOFTAI.Orchestration/Answering/Policies/TablePolicy.cs
src/TILSOFTAI.Orchestration/Answering/Policies/NoDataPolicy.cs
src/TILSOFTAI.Orchestration/Answering/Policies/FollowUpPolicy.cs
```

Suggested model:

```csharp
public sealed record AnswerPolicy
{
    public int MaxRowsForChat { get; init; } = 20;
    public int MaxRowsForNarration { get; init; } = 20;
    public SummaryPolicy Summary { get; init; } = new();
    public TablePolicy Table { get; init; } = new();
    public NoDataPolicy NoData { get; init; } = new();
    public FollowUpPolicy FollowUp { get; init; } = new();
}
```

Summary policy:

```csharp
public sealed record SummaryPolicy
{
    public string Mode { get; init; } = "ai"; // ai | fallback | disabled
    public string Style { get; init; } = "business_concise";
    public int MaxSentences { get; init; } = 4;
    public bool IncludeFilters { get; init; } = true;
    public bool IncludeRowCount { get; init; } = true;
    public bool IncludeCaveats { get; init; } = true;
    public IReadOnlyDictionary<string, string> InstructionsByLocale { get; init; } = new Dictionary<string, string>();
    public IReadOnlyList<string> ForbiddenClaims { get; init; } = [];
}
```

Acceptance:

```text
- Policy model is domain-neutral.
- Defaults are generic, not model-specific.
```

---

## Phase 2 — Extend SQL Answer Policy JSON

Update:

```text
sql/current/008_seed_model_capability_catalog.sql
sql/current/997_validate_capability_catalog.sql
```

Policy example:

```json
{
  "maxRowsForChat": 20,
  "maxRowsForNarration": 20,
  "summary": {
    "mode": "ai",
    "style": "business_concise",
    "maxSentences": 4,
    "includeFilters": true,
    "includeRowCount": true,
    "includeCaveats": true,
    "instructionsByLocale": {
      "vi-VN": "Tóm tắt dữ liệu ERP ngắn gọn cho người dùng nội bộ. Chỉ dùng dữ liệu được cung cấp. Không suy diễn.",
      "en-US": "Summarize ERP data concisely for internal users. Use only supplied data. Do not infer."
    },
    "forbiddenClaims": [
      "Do not infer causes.",
      "Do not recommend actions unless supported by data.",
      "Do not mention data not present in supplied rows.",
      "Do not expose hidden or masked fields."
    ]
  },
  "table": {
    "enabled": true,
    "maxDisplayedRows": 20,
    "includeRowCount": true,
    "includeTruncationNotice": true
  },
  "noData": {
    "includeUsedFilters": true
  },
  "followUp": {
    "includeMissingFields": true
  }
}
```

Acceptance:

```text
- All six model capabilities have valid policy JSON.
- Catalog validation fails if required policy sections are missing.
```

---

## Phase 3 — Map SQL Policy to Runtime Policy

Review:

```text
SqlCapabilityCatalogRepository.cs
CapabilityDescriptor.cs
AnswerComposerRequest.cs
```

Rules:

```text
- SQL policy JSON maps to `AnswerPolicy`.
- Invalid policy JSON fails catalog validation or runtime load.
- Runtime must not ignore policy silently.
```

Add tests:

```text
SqlBackedCatalog_MapsSummaryPolicy
SqlBackedCatalog_MapsTablePolicy
SqlBackedCatalog_RejectsInvalidAnswerPolicy
```

Acceptance:

```text
- AnswerComposer receives fully parsed policy.
```

---

## Phase 4 — Apply Policy in StructuredAnswerComposer

Use `AnswerPolicy` to control:

```text
- whether narrator is called
- max rows sent to narrator
- max table rows
- no-data filter display
- follow-up missing field display
- truncation notice
```

Rules:

```text
summary.mode = ai:
  call IAnswerNarrationService

summary.mode = fallback:
  call GenericSchemaSummaryFallback

summary.mode = disabled:
  use short generic text only

table.enabled = false:
  do not render table block

table.maxDisplayedRows:
  controls displayed rows

maxRowsForNarration:
  controls rows sent to AI narrator
```

Acceptance:

```text
- Changing policy changes composer behavior without C# change.
- Tests verify policy-driven behavior.
```

---

## Phase 5 — Locale-Specific Instructions

Narrator prompt builder must use:

```text
SummaryPolicy.InstructionsByLocale[request.Locale]
```

Fallback order:

```text
request.Locale
vi-VN
en-US
first available
generic default
```

Rules:

```text
- Instructions come from SQL catalog.
- Do not hardcode domain-specific instructions.
- Generic safety rules may remain in code.
```

Acceptance:

```text
- Tests verify vi-VN and en-US instructions are selected from policy.
```

---

## Phase 6 — Catalog Validation

Update:

```text
sql/current/997_validate_capability_catalog.sql
```

Validate:

```text
- Policy JSON exists for enabled capabilities.
- summary.mode is one of ai/fallback/disabled.
- maxRowsForChat > 0.
- maxRowsForNarration > 0.
- table.maxDisplayedRows > 0 when table.enabled = true.
- forbiddenClaims is valid JSON array if present.
- instructionsByLocale is valid JSON object if present.
```

Acceptance:

```text
- Bad policy seed fails SQL validation clearly.
```

---

## Phase 7 — Tests

Add tests:

```text
AnswerPolicy_SummaryModeAi_CallsNarrator
AnswerPolicy_SummaryModeFallback_UsesFallback
AnswerPolicy_SummaryModeDisabled_DoesNotCallNarrator
AnswerPolicy_TableDisabled_NoTableBlock
AnswerPolicy_TableMaxRows_Truncates
AnswerPolicy_NoData_IncludeFilters
AnswerPolicy_FollowUp_IncludeMissingFields
CatalogValidation_InvalidPolicy_Fails
```

Acceptance:

```text
- Policy drives runtime behavior.
- C# contains only generic fallback defaults.
```

---

## Phase 8 — Documentation

Update:

```text
README.md
docs/architecture/sql_backed_capability_catalog.md
docs/reports/model_e2e_framework_tuning_backlog.md
```

Add a section:

```text
How to configure answer behavior through SQL catalog
```

Include examples for:

```text
- enabling AI summary
- disabling AI summary
- fallback-only summary
- hiding table block
- changing max table rows
- changing locale instructions
```

---

## Final Validation

Run:

```bash
dotnet build
dotnet test
```

Run SQL migration and catalog validation.

Run model E2E smoke with at least:

```text
- summary.mode = ai
- summary.mode = fallback
```

## Definition of Done

```text
- Answer policy is SQL-backed.
- No capability-specific answer behavior is hardcoded in C#.
- Summary/table/no-data/follow-up policies are parsed and applied.
- Catalog validation catches invalid policies.
- Tests cover policy-driven composer behavior.
- No new business domain is added.
```
