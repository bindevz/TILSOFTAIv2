# Sprint 35 Runbook - Model Local AI Smoke Test

This runbook proves the Sprint 35 model-only route:

```text
API request -> SupervisorRuntime -> Official Microsoft Agent Framework -> local AI provider -> model tools -> CapabilityExecutionFacade -> AnswerComposer
```

## 1. Configure appsettings.Local.json

Create or update `src/TILSOFTAI.Api/appsettings.Local.json`.

```json
{
  "Auth": {
    "Enabled": false
  },
  "AiRouting": {
    "MicrosoftAgentFrameworkRoutingEnabled": true,
    "UseOfficialMicrosoftAgentFramework": true,
    "Provider": "OpenAiCompatibleLocal",
    "FallbackToLegacyPipeline": false,
    "AllowedDomains": [ "model" ],
    "MaxCandidateTools": 6,
    "MaxTotalCandidateTools": 6,
    "MaxCandidateToolsPerDomain": 6,
    "MaxDomainsPerRequest": 1,
    "ToolCallingRequired": true
  },
  "LocalAi": {
    "BaseUrl": "http://localhost:11434/v1",
    "Model": "YOUR_TOOL_CALLING_MODEL",
    "ApiKeyEnvironmentVariable": "TILSOFTAI_LOCAL_AI_API_KEY",
    "TimeoutSeconds": 120
  }
}
```

Replace `YOUR_TOOL_CALLING_MODEL` with a real local model that supports OpenAI-compatible tool/function calling. Do not leave `CHANGE_ME_TOOL_CALLING_MODEL`.

`LocalAi:BaseUrl` must be the OpenAI-compatible base URL used by the SDK client, usually ending in `/v1`. `LocalAi:ChatCompletionsPath` is not appended by the current SDK registration.

## 2. Set Local AI API Key

PowerShell:

```powershell
$env:TILSOFTAI_LOCAL_AI_API_KEY = "local-ai"
```

Use the real key if your local gateway requires one. For local gateways that ignore keys, any non-empty value is acceptable.

## 3. Start the API

```powershell
dotnet run --project src\TILSOFTAI.Api\TILSOFTAI.Api.csproj --urls http://localhost:5000
```

## 4. Check Health and Readiness

```powershell
Invoke-RestMethod http://localhost:5000/health/live
Invoke-RestMethod http://localhost:5000/health/ready
```

Expected readiness:

```text
Healthy when:
- AiRouting:Provider = OpenAiCompatibleLocal
- LocalAi:Model is a real non-placeholder tool-calling model
- IChatClient is registered
- AiRouting:AllowedDomains contains only model
- AiRouting:FallbackToLegacyPipeline = false
```

If `/health/ready` is unhealthy, inspect `official-agent-framework` in logs or `/health/detailed` with an authenticated request.

## 5. Send a Chat Request

Base request:

```powershell
$body = @{
  input = "Có bao nhiêu model?"
  preferredLanguage = "vi-VN"
  metadata = @{
    answerMode = "structured"
  }
} | ConvertTo-Json -Depth 5

Invoke-RestMethod `
  -Method Post `
  -Uri http://localhost:5000/api/chats `
  -ContentType "application/json" `
  -Body $body
```

## 6. Request raw_json Mode

```powershell
$body = @{
  input = "Cho tôi xem thông tin model ABC"
  preferredLanguage = "vi-VN"
  metadata = @{
    answerMode = "rawJson"
  }
} | ConvertTo-Json -Depth 5

Invoke-RestMethod -Method Post -Uri http://localhost:5000/api/chats -ContentType "application/json" -Body $body
```

Expected behavior:

```text
model_get_overview is selected.
Arguments include modelCode/model_code = ABC.
The response is composed by AnswerComposer in raw_json mode.
No legacy fallback is used.
```

## 7. Request structured Mode

```powershell
$body = @{
  input = "Show materials for model ABC"
  preferredLanguage = "en-US"
  metadata = @{
    answerMode = "structured"
  }
} | ConvertTo-Json -Depth 5

Invoke-RestMethod -Method Post -Uri http://localhost:5000/api/chats -ContentType "application/json" -Body $body
```

Expected behavior:

```text
model_get_materials is selected.
Arguments include modelCode/model_code = ABC.
The response is composed by AnswerComposer in structured mode.
No legacy fallback is used.
```

## 8. Required Smoke Prompts

Run this minimum set:

| Prompt | Expected behavior |
| --- | --- |
| `Có bao nhiêu model?` | `model_count` |
| `How many models are active?` | `model_count` |
| `Cho tôi xem thông tin model ABC` | `model_get_overview` with `modelCode`/`model_code` = `ABC` |
| `Show materials for model ABC` | `model_get_materials` with `modelCode`/`model_code` = `ABC` |
| `Model ABC gồm những piece nào?` | `model_get_pieces` with `modelCode`/`model_code` = `ABC` |
| `Cho tôi xem thông tin model` | Follow-up question; no SQL execution |

## 9. Repeatable API Smoke File

Run the documented HTTP smoke cases in:

```text
spec/Sprint_35/smoke/model_api_smoke.http
```

The file covers:

```text
1. Model count, structured, vi-VN.
2. Model overview, raw_json, vi-VN.
3. Missing model parameter, structured, vi-VN.
4. Materials by model code, structured, en-US.
```

The companion checklist is:

```text
spec/Sprint_35/smoke/README.md
```

## 10. Logs to Inspect

For each call, inspect structured logs and tool routing traces for:

```text
correlationId
provider
model
allowedDomains
candidateCapabilities
advertisedFunctionNames
selectedFunctionName
selectedArguments
capabilityKey
procedureName
rowCount
durationMs
answerMode
fallbackUsed=false
```

The smoke is successful only when one API request reaches `OfficialAgentToolRouter`, creates an official `AIAgent`, advertises model-only function tools, invokes an `AIFunction`, calls `CapabilityExecutionFacade`, avoids direct SQL in the function callback, passes through `AnswerComposer`, and does not use legacy fallback.
