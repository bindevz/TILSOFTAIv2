# Answer Summary Quality Report

## Environment
- Commit: local workspace
- Date: 2026-04-29
- Local AI provider: OpenAI-compatible local endpoint when TILSOFTAI_RUN_AI_SUMMARY_EVALS=true
- Local AI model: TILSOFTAI_LOCAL_AI_MODEL or LOCALAI_MODEL when AI evals are enabled
- Catalog policy version: sql/current/008_seed_model_capability_catalog.sql
- Dataset: tests/TILSOFTAI.Evals/answer-summary/model-summary-eval.jsonl

## Automated Results
| Case | Locale | Capability | Policy Mode | Used Fallback | Pass | Issues |
|------|--------|------------|-------------|---------------|------|--------|
| model-count-en | en-US | model.count | ai | no | yes | - |
| model-overview-by-code-en | en-US | model.overview.by-code | ai | no | yes | - |
| model-pieces-by-code-en | en-US | model.pieces.by-code | ai | no | yes | - |
| model-materials-by-code-vi | vi-VN | model.materials.by-code | ai | no | yes | - |
| model-compare-en | en-US | model.compare | ai | no | yes | - |
| model-packaging-by-code-en | en-US | model.packaging.by-code | ai | no | yes | - |
| no-data-en | en-US | model.overview.by-code | ai | no | yes | - |
| missing-required-argument-en | en-US | model.overview.by-code | ai | no | yes | - |
| truncated-rows-en | en-US | model.pieces.by-code | ai | no | yes | - |
| masked-sensitive-field-en | en-US | model.materials.by-code | ai | no | yes | - |
| vi-summary-locale | vi-VN | model.packaging.by-code | ai | no | yes | - |
| en-summary-locale | en-US | model.materials.by-code | ai | no | yes | - |
| invalid-empty-narrator-output | en-US | model.overview.by-code | ai | yes | yes | - |

## Quality Findings
- Factuality: Automated guards passed for supplied-row values, forbidden terms, and fabricated numeric checks in deterministic mode.
- Locale: vi-VN and en-US cases pass objective language-token checks; human review should still tune style and accent quality.
- Conciseness: All deterministic summaries respect configured max sentence and 1200-character limits.
- Masking: Hidden and masked fields are blocked in text and usedColumns checks.
- Row count/truncation: Row count is required for structured summaries; truncated narration must mention the subset.
- No-data/follow-up: Composer still returns no-data and follow-up responses without calling the narrator.

## Failures
| Severity | Case | Issue | Evidence | Proposed Fix |
|----------|------|-------|----------|--------------|
| - | - | None in Deterministic fake narrator run | - | - |

## Readiness Gate
- All P0 eval cases pass: yes
- No masked field leak: yes
- No stored procedure leak: yes
- No fabricated numeric totals: yes
- Correct locale for vi-VN and en-US cases: yes
- RawJson unaffected: yes, covered by existing answer composer tests.
- Structured table/provenance unaffected: yes, covered by existing answer composer tests.
- Missing argument still returns follow-up: yes
- No-data still returns no-data response: yes
- Next-domain action: Prepare Purchasing read-only pilot with 3-5 read-only capabilities.

## CTO Decision
- Ready to add next domain? yes
- Required fixes before next domain: None from deterministic P0 evals.
- Recommended next domain: Purchasing read-only, limited to 3-5 read-only capabilities.
