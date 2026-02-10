using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Conversations;
using TILSOFTAI.Orchestration.Llm;
using TILSOFTAI.Orchestration.Sql;
using TILSOFTAI.Orchestration.Tools;

namespace TILSOFTAI.Orchestration.Modules;

public sealed class ModuleScopeResolver : IModuleScopeResolver
{
    private readonly ILlmClient _llmClient;
    private readonly ISqlExecutor _sqlExecutor;
    private readonly ILogger<ModuleScopeResolver> _logger;

    // Fallback: if LLM fails or returns nothing, use all modules
    private static readonly ModuleScopeResult FallbackResult = new()
    {
        Modules = new[] { "model", "analytics", "platform" },
        Confidence = 0m,
        Reasons = new[] { "Fallback: LLM scope resolution failed, using all modules" }
    };

    public ModuleScopeResolver(
        ILlmClient llmClient,
        ISqlExecutor sqlExecutor,
        ILogger<ModuleScopeResolver> logger)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _sqlExecutor = sqlExecutor ?? throw new ArgumentNullException(nameof(sqlExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ModuleScopeResult> ResolveAsync(
        string userQuery,
        TilsoftExecutionContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            // 1. Load module catalog from SQL
            var modules = await LoadModuleCatalogAsync(context, cancellationToken);
            if (modules.Count == 0)
            {
                _logger.LogWarning("ModuleCatalog is empty. Using fallback.");
                return FallbackResult;
            }

            // If only 1 non-platform module, skip LLM call
            var nonPlatform = modules.Where(m => 
                !string.Equals(m.ModuleKey, "platform", StringComparison.OrdinalIgnoreCase)).ToList();
            if (nonPlatform.Count <= 1)
            {
                return new ModuleScopeResult
                {
                    Modules = nonPlatform.Select(m => m.ModuleKey).ToList(),
                    Confidence = 1.0m,
                    Reasons = new[] { "Only one non-platform module available" }
                };
            }

            // 2. Build scope prompt
            var prompt = BuildScopePrompt(userQuery, modules);

            // 3. Ask LLM to select modules
            var llmRequest = new LlmRequest
            {
                SystemPrompt = "You are a module router. Given a user query and available modules, select which modules are needed. Respond ONLY in valid JSON with no markdown or extra text. Format: {\"modules\":[\"module1\"],\"confidence\":0.9,\"reasons\":[\"reason\"]}",
                Messages = new List<LlmMessage> { new(ChatRoles.User, prompt) },
                Tools = Array.Empty<ToolDefinition>(),
                MaxTokens = 200
            };

            var response = await _llmClient.CompleteAsync(llmRequest, cancellationToken);
            var result = ParseScopeResponse(response.Content, modules);

            _logger.LogInformation(
                "ModuleScopeResolved | Query: {QueryPreview} | Modules: [{Modules}] | Confidence: {Confidence}",
                userQuery.Length > 80 ? userQuery[..80] + "..." : userQuery,
                string.Join(", ", result.Modules),
                result.Confidence);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ModuleScopeResolver failed. Using fallback.");
            return FallbackResult;
        }
    }

    private async Task<List<ModuleCatalogEntry>> LoadModuleCatalogAsync(
        TilsoftExecutionContext context,
        CancellationToken cancellationToken)
    {
        var language = string.IsNullOrWhiteSpace(context.Language) ? "en" : context.Language;
        var parameters = new Dictionary<string, object?>
        {
            ["@TenantId"] = context.TenantId,
            ["@Language"] = language
        };

        var rows = await _sqlExecutor.ExecuteQueryAsync(
            "dbo.app_modulecatalog_list", parameters, cancellationToken);

        return rows.Select(row => new ModuleCatalogEntry
        {
            ModuleKey = row.TryGetValue("ModuleKey", out var mk) ? mk?.ToString() ?? "" : "",
            AppKey = row.TryGetValue("AppKey", out var ak) ? ak?.ToString() ?? "" : "",
            Instruction = row.TryGetValue("Instruction", out var inst) ? inst?.ToString() ?? "" : "",
            Priority = row.TryGetValue("Priority", out var pri) ? Convert.ToInt32(pri) : 100
        }).ToList();
    }

    private static string BuildScopePrompt(string query, List<ModuleCatalogEntry> modules)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Given this user query, select the relevant modules.");
        sb.Append("Query: \"").Append(query).AppendLine("\"");
        sb.AppendLine("Available modules:");
        foreach (var m in modules)
        {
            if (string.Equals(m.ModuleKey, "platform", StringComparison.OrdinalIgnoreCase))
                continue; // Platform is always included, don't confuse LLM
            sb.Append("- ").Append(m.ModuleKey).Append(": ").AppendLine(m.Instruction);
        }
        sb.AppendLine("Rules:");
        sb.AppendLine("- Select ONLY modules needed for the query");
        sb.AppendLine("- If unsure, include the module (better to include than miss)");
        sb.AppendLine("- 'platform' module is always auto-included, do NOT list it");
        sb.AppendLine("Respond JSON only: {\"modules\":[...],\"confidence\":0.0-1.0,\"reasons\":[...]}");
        return sb.ToString();
    }

    private static ModuleScopeResult ParseScopeResponse(
        string? content, List<ModuleCatalogEntry> availableModules)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return FallbackResult;
        }

        try
        {
            // Strip markdown code fences if present
            var json = content.Trim();
            if (json.StartsWith("```"))
            {
                var firstNewline = json.IndexOf('\n');
                var lastFence = json.LastIndexOf("```");
                if (firstNewline > 0 && lastFence > firstNewline)
                {
                    json = json[(firstNewline + 1)..lastFence].Trim();
                }
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var modules = new List<string>();
            if (root.TryGetProperty("modules", out var modulesEl) && modulesEl.ValueKind == JsonValueKind.Array)
            {
                var validKeys = new HashSet<string>(
                    availableModules.Select(m => m.ModuleKey), 
                    StringComparer.OrdinalIgnoreCase);

                foreach (var item in modulesEl.EnumerateArray())
                {
                    var key = item.GetString();
                    if (!string.IsNullOrEmpty(key) && validKeys.Contains(key))
                    {
                        modules.Add(key);
                    }
                }
            }

            var confidence = 0m;
            if (root.TryGetProperty("confidence", out var confEl))
            {
                confidence = confEl.TryGetDecimal(out var c) ? c : 0m;
            }

            var reasons = new List<string>();
            if (root.TryGetProperty("reasons", out var reasonsEl) && reasonsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in reasonsEl.EnumerateArray())
                {
                    var reason = item.GetString();
                    if (!string.IsNullOrEmpty(reason))
                    {
                        reasons.Add(reason);
                    }
                }
            }

            // Validate: if LLM returned empty modules, fallback
            if (modules.Count == 0)
            {
                return FallbackResult;
            }

            return new ModuleScopeResult
            {
                Modules = modules,
                Confidence = confidence,
                Reasons = reasons
            };
        }
        catch (JsonException)
        {
            return FallbackResult;
        }
    }

    private sealed class ModuleCatalogEntry
    {
        public string ModuleKey { get; init; } = "";
        public string AppKey { get; init; } = "";
        public string Instruction { get; init; } = "";
        public int Priority { get; init; }
    }
}
