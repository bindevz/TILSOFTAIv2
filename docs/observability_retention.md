# Observability Retention Policy

## Default retention
- Keep Conversation, ConversationMessage, ToolExecution, and ErrorLog rows for 90 days by default.
- Extend or shorten retention per tenant based on contract and regulatory requirements.

## Purge job pattern
- Run a scheduled SQL job daily during low-traffic hours.
- Delete in batches (for example, 10k rows per batch) to avoid long locks.
- Purge order (oldest first):
  1) ConversationMessage
  2) ToolExecution
  3) ErrorLog
  4) Conversation
- Filter by TenantId and CreatedAtUtc when applicable.

## PII policy
- Avoid writing raw PII into observability tables unless explicitly required.
- If PII is unavoidable, ensure encryption at rest and strict access controls.
- Consider masking or hashing identifiers in payloads.

## JSON payload candidates (future SQL 2025 JSON type)
- PayloadJson
- ArgumentsJson
- ResultJson
- DetailJson

## Notes
- Current columns remain nvarchar(max) until the SQL 2025 JSON type upgrade in PATCH_06.
