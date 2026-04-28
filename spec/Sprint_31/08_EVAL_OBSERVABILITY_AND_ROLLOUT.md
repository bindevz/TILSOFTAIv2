# Phase 8 — Evaluation, Observability, and Rollout

## Goal

Add regression tests, routing evaluation, telemetry, and rollout controls so the Agent Framework routing path can be deployed safely.

## Evaluation datasets

Create:

```text
tests/TILSOFTAI.Evals/tool-routing.vi.jsonl
tests/TILSOFTAI.Evals/tool-routing.en.jsonl
tests/TILSOFTAI.Evals/argument-extraction.vi.jsonl
tests/TILSOFTAI.Evals/argument-extraction.en.jsonl
tests/TILSOFTAI.Evals/answer-composer.jsonl
tests/TILSOFTAI.Evals/write-preview.jsonl
```

## Routing examples

Vietnamese:

```json
{
  "utterance": "Tồn mã MADEIRA-BLK ở kho BD còn bao nhiêu?",
  "locale": "vi-VN",
  "expectedDomain": "warehouse",
  "expectedFunction": "warehouse_inventory_by_item",
  "expectedArguments": {
    "item_no": "MADEIRA-BLK",
    "warehouse_code": "BD"
  },
  "shouldExecute": true
}
```

English:

```json
{
  "utterance": "Show open purchase orders for supplier ABC last month",
  "locale": "en-US",
  "expectedDomain": "purchasing",
  "expectedFunction": "purchasing_open_purchase_orders_by_supplier",
  "expectedArguments": {
    "supplier_code": "ABC",
    "date_range": "previous_month"
  },
  "shouldExecute": true
}
```

Write preview:

```json
{
  "utterance": "Tạo PO cho supplier ABC item A123 số lượng 500",
  "locale": "vi-VN",
  "expectedDomain": "purchasing",
  "expectedFunction": "purchasing_po_create_preview",
  "requiresConfirmation": true,
  "shouldExecuteWriteImmediately": false
}
```

Missing argument:

```json
{
  "utterance": "Xem công nợ khách hàng",
  "locale": "vi-VN",
  "expectedDomain": "accounting",
  "expectedFunction": "accounting_receivables_by_customer",
  "shouldExecute": false,
  "expectedClarification": true
}
```

## Metrics

Track:

```text
function_selection_accuracy
argument_extraction_accuracy
missing_required_argument_detection
ambiguous_entity_detection
false_read_execution
false_write_execution
unauthorized_execution
clarification_rate
answer_format_accuracy
latency_by_stage
adapter_failure_rate
```

Acceptance targets:

```text
function_selection_accuracy >= 90%
argument_extraction_accuracy >= 85%
missing_required_argument_detection >= 95%
false_write_execution = 0%
unauthorized_execution = 0%
```

## Telemetry and audit

Log:

```text
correlationId
tenantId
userId
locale
user message hash or redacted message
detected hard signals
candidate domains
candidate capabilities
advertised function tools
selected function
arguments before normalization
arguments after normalization
validation result
adapter type
row count
answer mode
latency per stage
model/provider
success/failure
error code
```

Secure audit/debug may include stored procedure name. Normal logs should not expose internal procedure names unless policy allows it.

Never log:

```text
secrets
credentials
full sensitive raw rows
unmasked financial/PII fields when policy forbids
```

## Rollout strategy

Use phased rollout:

```text
Stage 1: feature flag off by default, internal dev only
Stage 2: enable for one tenant and 5 priority read-only capabilities
Stage 3: enable for selected internal users
Stage 4: enable raw JSON and structured answer modes
Stage 5: enable write preview tools, but not write execution
Stage 6: enable confirmation + approval execution
Stage 7: expand domains and capabilities
```

## PR checklist

Before submitting changes:

- [ ] Feature flag off keeps legacy behavior.
- [ ] Candidate tool list is capped.
- [ ] No function tool directly calls SQL.
- [ ] Dynamic descriptions come from SQL KB.
- [ ] Domain retrieval works for Vietnamese and English.
- [ ] Required args missing returns clarification.
- [ ] Ambiguous entity returns user selection/follow-up.
- [ ] Read-only capability executes through facade.
- [ ] Write capability only produces preview before confirmation.
- [ ] Actual write execution requires approval.
- [ ] Answer composer supports `RawJson` and `Structured`.
- [ ] Sensitive fields are masked before AI summary.
- [ ] Tool routing trace is persisted.
- [ ] Eval tests added for priority domains.

## Final target state

```text
SQL Server 2025 Semantic KB
  -> multilingual domain/capability/argument retrieval
  -> dynamic function descriptions
  -> entity aliases and embeddings

Microsoft Agent Framework
  -> selected function tools only
  -> natural language to tool + params
  -> optional human-in-the-loop support

CapabilityExecutionFacade
  -> source-of-truth policy/validation/approval
  -> adapter execution boundary

AnswerComposer
  -> raw JSON OR structured answer
  -> text/table/chart/summary/follow-up/confirmation
```

End state: Microsoft Agent Framework is the brain for tool and parameter selection; SQL Server 2025 is the semantic memory/control plane; existing TILSOFTAI governance remains the execution safety boundary.
