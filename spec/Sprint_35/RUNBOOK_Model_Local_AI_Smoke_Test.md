# Model Local AI Smoke Test

## 1. Local Settings

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

Use a real OpenAI-compatible tool-calling model. Do not leave `CHANGE_ME_TOOL_CALLING_MODEL` or another placeholder value.

## 2. Environment Variable

```powershell
$env:TILSOFTAI_LOCAL_AI_API_KEY = "local-ai"
```

Use the real key if your local gateway requires one.

## 3. Start API

```powershell
dotnet run --project src\TILSOFTAI.Api\TILSOFTAI.Api.csproj --urls http://localhost:5000
```

## 4. Health Check

```powershell
Invoke-RestMethod http://localhost:5000/health/live
Invoke-RestMethod http://localhost:5000/health/ready
```

Readiness should be healthy when `AiRouting:Provider` is `OpenAiCompatibleLocal`, `LocalAi:Model` is a real non-placeholder model, `IChatClient` is registered, `AllowedDomains` is only `model`, and `FallbackToLegacyPipeline` is `false`.

## 5. 5 Smoke Prompts

Send each prompt to `POST http://localhost:5000/api/chats` with `preferredLanguage` and `metadata.answerMode`.

| Prompt | Language | Answer mode | Expected behavior |
| --- | --- | --- | --- |
| `Có bao nhiêu model?` | `vi-VN` | `structured` | Selects `model_count`; no legacy fallback. |
| `How many models are active?` | `en-US` | `structured` | Selects `model_count`; no legacy fallback. |
| `Cho tôi xem thông tin model ABC` | `vi-VN` | `rawJson` | Selects overview by code with `modelCode = "ABC"`; raw envelope only. |
| `Show materials for model ABC` | `en-US` | `structured` | Selects materials by code with `modelCode = "ABC"`; blocks and provenance returned. |
| `Cho tôi xem thông tin model` | `vi-VN` | `structured` | Returns a follow-up for missing `modelCode`; no SQL execution. |

Example body:

```powershell
$body = @{
  input = "Cho tôi xem thông tin model ABC"
  preferredLanguage = "vi-VN"
  metadata = @{
    answerMode = "rawJson"
  }
} | ConvertTo-Json -Depth 5

Invoke-RestMethod `
  -Method Post `
  -Uri http://localhost:5000/api/chats `
  -ContentType "application/json" `
  -Body $body
```

## 6. Expected Logs

For each request, logs and routing traces should include:

```text
correlationId
tenantId
userId
conversationId
provider
model
candidate capability keys
advertised tool names
selected function
capability key
row count
answer mode
fallback used = false
```
