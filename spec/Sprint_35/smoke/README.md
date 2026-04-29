# Sprint 35 API Smoke Cases

Use `model_api_smoke.http` to prove the model-only runtime through the public API, not only through unit tests.

## Prerequisites

1. Configure `src/TILSOFTAI.Api/appsettings.Local.json` with the Sprint 35 local AI settings from `spec/Sprint_35/RUNBOOK_Model_Local_AI_Smoke_Test.md`.
2. Set `TILSOFTAI_LOCAL_AI_API_KEY` to a non-empty value or the real local gateway key.
3. Start the API:

```powershell
dotnet run --project src\TILSOFTAI.Api\TILSOFTAI.Api.csproj --urls http://localhost:5000
```

4. Run the requests in `model_api_smoke.http`.

## Required Cases

| Case | Request | Expected capability | Expected response |
| --- | --- | --- | --- |
| 1 | `Có bao nhiêu model?`, `structured`, `vi-VN` | `model.count` | Structured answer with text, blocks, and provenance. |
| 2 | `Cho tôi xem thông tin model ABC`, `rawJson`, `vi-VN` | `model.overview.by-code` | Raw JSON envelope; no final LLM summary. |
| 3 | `Cho tôi xem thông tin model`, `structured`, `vi-VN` | none executed | Follow-up asking for `modelCode`; no SQL execution. |
| 4 | `Show materials for model ABC`, `structured`, `en-US` | `model.materials.by-code` | Structured answer with text, blocks, and provenance. |

## Response Checks

Every successful API response should include:

```json
{
  "success": true,
  "content": "...",
  "conversationId": "...",
  "correlationId": "...",
  "traceId": "...",
  "language": "vi-VN"
}
```

For `rawJson` mode, `content` is expected to be empty when the structured answer detail is carried through the internal `RawJsonBlock`; inspect logs and traces for the deterministic raw envelope. For structured mode, `content` should contain a user-facing summary. If the API shape is later extended to return `Detail`, verify the raw envelope contains `mode`, `capabilityKey`, `procedureName`, `arguments`, `rowCount`, `rows`, `executionMetadata`, and `provenance`.

## Log Checks

For each request, filter logs by the returned `correlationId`. The request passes only when the same correlation id can be traced through:

```text
agent_route_started
candidate_selection_completed
tools_advertised
agent_created
agent_run_started
agent_tool_invoked
capability_facade_started
capability_execution_completed
answer_composer_started
answer_composer_completed
```

The missing-parameter case should include `answer_composer_completed` with `answerType=follow_up` and must not include a successful SQL execution event.

## Required Fields

The logs or stored routing trace should show these fields where applicable:

```text
correlationId
tenantId
userId
conversationId
provider
model
allowedDomains
candidateCapabilityKeys
advertisedFunctionNames
selectedFunctionName
capabilityKey
procedureName
argumentsMasked
rowCount
durationMs
answerMode
fallbackUsed=false
errorCode
```

## Fail-Closed Check

Temporarily misconfigure one of these settings in `appsettings.Local.json`:

```json
{
  "AiRouting": {
    "FallbackToLegacyPipeline": false,
    "AllowedDomains": [ "model" ]
  },
  "LocalAi": {
    "Model": "CHANGE_ME_TOOL_CALLING_MODEL"
  }
}
```

Expected behavior:

```text
/health/ready is unhealthy, or the chat request fails closed with a stable error code and correlation id.
No legacy keyword or regex pipeline response is returned.
Logs include agent_route_failed_closed or health-check detail explaining the misconfiguration.
```

