using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Analytics;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Conversations;
using TILSOFTAI.Orchestration.Llm;
using TILSOFTAI.Orchestration.Tools;

namespace TILSOFTAI.Orchestration.Analytics;

/// <summary>
/// Deterministic orchestrator for deep analytics workflow.
/// PATCH 29.02: Enforces fixed tool order: catalog_search → get_dataset → plan → validate → execute → assemble → render.
/// PATCH 29.06: Adds persistence and caching.
/// </summary>
public sealed class AnalyticsOrchestrator
{
    private readonly IToolCatalogResolver _toolCatalogResolver;
    private readonly IToolHandler _toolHandler;
    private readonly ToolGovernance _toolGovernance;
    private readonly ILlmClient _llmClient;
    private readonly IInsightAssemblyService _insightAssemblyService;
    private readonly InsightRenderer _renderer;
    private readonly AnalyticsPersistence _persistence;
    private readonly AnalyticsCache _cache;
    private readonly ICacheWriteQueue _cacheWriteQueue;
    private readonly AnalyticsOptions _options;
    private readonly ILogger<AnalyticsOrchestrator> _logger;

    private const string ToolCatalogSearch = "catalog_search";
    private const string ToolCatalogGetDataset = "catalog_get_dataset";
    private const string ToolValidatePlan = "analytics_validate_plan";
    private const string ToolExecutePlan = "analytics_execute_plan";

    public AnalyticsOrchestrator(
        IToolCatalogResolver toolCatalogResolver,
        IToolHandler toolHandler,
        ToolGovernance toolGovernance,
        ILlmClient llmClient,
        IInsightAssemblyService insightAssemblyService,
        InsightRenderer renderer,
        AnalyticsPersistence persistence,
        AnalyticsCache cache,
        ICacheWriteQueue cacheWriteQueue,
        IOptions<AnalyticsOptions> options,
        ILogger<AnalyticsOrchestrator> logger)
    {
        _toolCatalogResolver = toolCatalogResolver ?? throw new ArgumentNullException(nameof(toolCatalogResolver));
        _toolHandler = toolHandler ?? throw new ArgumentNullException(nameof(toolHandler));
        _toolGovernance = toolGovernance ?? throw new ArgumentNullException(nameof(toolGovernance));
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _insightAssemblyService = insightAssemblyService ?? throw new ArgumentNullException(nameof(insightAssemblyService));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _cacheWriteQueue = cacheWriteQueue ?? throw new ArgumentNullException(nameof(cacheWriteQueue));
        _options = options?.Value ?? new AnalyticsOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Execute analytics workflow with deterministic tool ordering.
    /// </summary>
    public async Task<AnalyticsOrchestratorResult> ExecuteAsync(
        string userQuery,
        AnalyticsIntentResult intent,
        TilsoftExecutionContext context,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var toolCallCount = 0;
        var language = intent?.DetectedLanguage ?? context.Language ?? "en";
        var toolSequence = new List<string>();

        _logger.LogInformation(
            "AnalyticsOrchestrator.Started | CorrelationId: {CorrelationId} | Query: {Query} | Language: {Language} | Confidence: {Confidence}",
            context.CorrelationId, userQuery, language, intent?.Confidence ?? 0);

        try
        {
            // PATCH 29.06: Check cache first
            // PATCH 30.03: Include roles for security-isolated cache
            var cachedInsight = await _cache.TryGetAsync(context.TenantId, userQuery, context.Roles, ct);
            if (cachedInsight != null)
            {
                var cachedOutput = _renderer.Render(cachedInsight, language);
                sw.Stop();
                _logger.LogInformation(
                    "AnalyticsOrchestrator.CacheHit | CorrelationId: {CorrelationId} | Duration: {DurationMs}ms | CacheHit: true",
                    context.CorrelationId, sw.ElapsedMilliseconds);

                return new AnalyticsOrchestratorResult
                {
                    Success = true,
                    Content = cachedOutput,
                    ToolCallCount = 0,
                    ToolSequence = new List<string> { "cache_hit" },
                    DurationMs = sw.ElapsedMilliseconds
                };
            }

            // PATCH 29.06: Persist TaskFrame
            var requestId = Guid.NewGuid().ToString("N");
            _ = _persistence.SaveTaskFrameAsync(
                context.TenantId,
                context.ConversationId,
                requestId,
                "analytics",
                null, // entity filled later
                null, // metrics filled later
                null, // filters filled later
                null, // breakdowns filled later
                null, // timeRange
                false,
                intent?.Confidence,
                ct);

            var tools = await _toolCatalogResolver.GetResolvedToolsAsync(ct);
            var toolLookup = tools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

            // Step 1: Catalog Search
            if (toolCallCount >= _options.MaxToolCallsPerTurn)
                return CreateLimitExceeded(toolSequence, sw.ElapsedMilliseconds);

            var searchQuery = ExtractSearchQuery(userQuery, intent);
            var searchArgs = JsonSerializer.Serialize(new { query = searchQuery });
            var searchResult = await ExecuteToolAsync(ToolCatalogSearch, searchArgs, toolLookup, context, ct);
            toolCallCount++;
            toolSequence.Add(ToolCatalogSearch);

            var datasetKey = ExtractDatasetKey(searchResult);
            if (string.IsNullOrEmpty(datasetKey))
            {
                return CreateNoDatasetFound(toolSequence, sw.ElapsedMilliseconds, language);
            }

            // Step 2: Get Dataset Schema
            if (toolCallCount >= _options.MaxToolCallsPerTurn)
                return CreateLimitExceeded(toolSequence, sw.ElapsedMilliseconds);

            var getDatasetArgs = JsonSerializer.Serialize(new { datasetKey });
            var schemaResult = await ExecuteToolAsync(ToolCatalogGetDataset, getDatasetArgs, toolLookup, context, ct);
            toolCallCount++;
            toolSequence.Add(ToolCatalogGetDataset);

            // Step 3: Generate Plan via LLM (short focused prompt)
            var planJson = await GeneratePlanAsync(userQuery, schemaResult, language, context, ct);
            
            // Step 4: Validate Plan (with retries)
            string? validatedPlan = null;
            for (var retry = 0; retry <= _options.MaxPlanRetries; retry++)
            {
                if (toolCallCount >= _options.MaxToolCallsPerTurn)
                    return CreateLimitExceeded(toolSequence, sw.ElapsedMilliseconds);

                var validateArgs = JsonSerializer.Serialize(new { planJson = JsonSerializer.Deserialize<JsonElement>(planJson) });
                var validationResult = await ExecuteToolAsync(ToolValidatePlan, validateArgs, toolLookup, context, ct);
                toolCallCount++;
                toolSequence.Add(ToolValidatePlan);

                var validation = ParseValidation(validationResult);
                if (validation.IsValid)
                {
                    validatedPlan = planJson;
                    var planHash = ComputePlanHash(planJson);
                    _logger.LogInformation(
                        "AnalyticsOrchestrator.PlanValidated | CorrelationId: {CorrelationId} | PlanHash: {PlanHash} | RetryCount: {RetryCount}",
                        context.CorrelationId, planHash, retry);
                    break;
                }

                if (!validation.Retryable || retry == _options.MaxPlanRetries)
                {
                    // PATCH 29.06: Persist validation error
                    _ = _persistence.SaveValidationErrorAsync(
                        context.TenantId,
                        requestId,
                        validation.ErrorCode ?? "VALIDATION_FAILED",
                        validation.ErrorMessage,
                        null,
                        planJson,
                        validation.Retryable,
                        retry,
                        ct);

                    return CreateValidationFailed(validation.ErrorMessage, toolSequence, sw.ElapsedMilliseconds, language);
                }

                // Retry: regenerate plan with error context
                planJson = await RegeneratePlanAsync(userQuery, schemaResult, validation.ErrorMessage, language, context, ct);
            }

            if (validatedPlan == null)
            {
                return CreateValidationFailed("Plan validation exhausted retries", toolSequence, sw.ElapsedMilliseconds, language);
            }

            // Step 5: Execute Plan
            if (toolCallCount >= _options.MaxToolCallsPerTurn)
                return CreateLimitExceeded(toolSequence, sw.ElapsedMilliseconds);

            var executePlanArgs = validatedPlan;
            var executeResult = await ExecuteToolAsync(ToolExecutePlan, executePlanArgs, toolLookup, context, ct);
            toolCallCount++;
            toolSequence.Add(ToolExecutePlan);

            // Step 6: Assemble Insight
            // PATCH 30.05: Pass validatedPlan for notes fidelity
            var queryResults = ParseQueryResults(executeResult);
            var taskFrame = BuildTaskFrame(userQuery, intent);
            var insight = await _insightAssemblyService.AssembleAsync(taskFrame, queryResults, validatedPlan, context, ct);

            // Step 7: Render
            var renderedOutput = _renderer.Render(insight, language);

            // PATCH 31.05: Safe background cache write via Channel queue
            _cacheWriteQueue.TryEnqueue(new CacheWriteItem(
                context.TenantId, userQuery, context.Roles, insight));

            sw.Stop();
            var finalPlanHash = ComputePlanHash(validatedPlan ?? "");
            _logger.LogInformation(
                "AnalyticsOrchestrator.Completed | CorrelationId: {CorrelationId} | ToolCalls: {ToolCalls} | Duration: {DurationMs}ms | CacheHit: false | PlanHash: {PlanHash} | Sequence: {Sequence}",
                context.CorrelationId, toolCallCount, sw.ElapsedMilliseconds, finalPlanHash, string.Join(" → ", toolSequence));

            return new AnalyticsOrchestratorResult
            {
                Success = true,
                Content = renderedOutput,
                ToolCallCount = toolCallCount,
                ToolSequence = toolSequence,
                DurationMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex,
                "AnalyticsOrchestrator.Failed | CorrelationId: {CorrelationId} | Duration: {DurationMs}ms | Error: {Error}",
                context.CorrelationId, sw.ElapsedMilliseconds, ex.Message);

            return new AnalyticsOrchestratorResult
            {
                Success = false,
                ErrorMessage = language == "vi" 
                    ? "Không thể hoàn thành phân tích. Vui lòng thử lại." 
                    : "Unable to complete analytics. Please try again.",
                ToolCallCount = toolCallCount,
                ToolSequence = toolSequence,
                DurationMs = sw.ElapsedMilliseconds
            };
        }
    }

    /// <summary>
    /// PATCH 31.01: Execute tool through unified governance pipeline.
    /// All tool calls (LLM-driven or orchestrator-driven) go through same governance.
    /// </summary>
    private async Task<string> ExecuteToolAsync(
        string toolName,
        string argsJson,
        Dictionary<string, ToolDefinition> toolLookup,
        TilsoftExecutionContext context,
        CancellationToken ct)
    {
        _logger.LogDebug("ExecutingTool | Name: {ToolName} | Governed: true", toolName);

        var govResult = await _toolGovernance.ValidateAndExecuteAsync(
            toolName, argsJson, toolLookup, context, _toolHandler, ct);

        if (!govResult.IsAllowed)
        {
            _logger.LogWarning(
                "ToolGovernanceDenied | Name: {ToolName} | Reason: {Reason} | Code: {Code}",
                toolName, govResult.DenialReason, govResult.DenialCode);

            // Return structured error JSON instead of throwing
            return JsonSerializer.Serialize(new
            {
                error = true,
                code = govResult.DenialCode ?? "GOVERNANCE_DENIED",
                message = govResult.DenialReason
            });
        }

        _logger.LogDebug(
            "ToolComplete | Name: {ToolName} | ResultLength: {Length} | Governed: true",
            toolName, govResult.Result?.Length ?? 0);

        return govResult.Result ?? "{}";
    }

    private static string ExtractSearchQuery(string userQuery, AnalyticsIntentResult? intent)
    {
        // Extract entity hints from intent for focused search
        var entityHints = intent?.Hints
            .Where(h => h.StartsWith("entity:"))
            .Select(h => h["entity:".Length..])
            .ToList() ?? new List<string>();

        if (entityHints.Count > 0)
        {
            return string.Join(" ", entityHints);
        }

        // Fallback: extract key words from query
        var words = userQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2)
            .Take(3);
        return string.Join(" ", words);
    }

    private static string? ExtractDatasetKey(string searchResult)
    {
        try
        {
            using var doc = JsonDocument.Parse(searchResult);
            var datasets = doc.RootElement.GetProperty("datasets");
            if (datasets.ValueKind == JsonValueKind.Array && datasets.GetArrayLength() > 0)
            {
                return datasets[0].GetProperty("DatasetKey").GetString();
            }
        }
        catch
        {
            // Parse failed
        }
        return null;
    }

    private async Task<string> GeneratePlanAsync(
        string query,
        string schema,
        string language,
        TilsoftExecutionContext context,
        CancellationToken ct)
    {
        var prompt = $@"Generate a JSON analytics plan for this query: ""{query}""

Schema:
{schema}

Requirements:
- Return ONLY valid JSON with: datasetKey, metrics (array with field, op, alias), groupBy (array), where (optional), limit
- Allowed ops: count, countDistinct, sum, avg, min, max
- Max 3 metrics, max 4 groupBy
- If query mentions season/mùa, add where filter for seasonCode

JSON:";

        var request = new LlmRequest
        {
            Messages = new List<LlmMessage>
            {
                new(ChatRoles.User, prompt)
            }
        };

        var response = await _llmClient.CompleteAsync(request, ct);
        return ExtractJson(response.Content ?? "{}");
    }

    private async Task<string> RegeneratePlanAsync(
        string query,
        string schema,
        string? errorMessage,
        string language,
        TilsoftExecutionContext context,
        CancellationToken ct)
    {
        var prompt = $@"Fix this analytics plan. Previous error: {errorMessage}

Query: ""{query}""

Schema:
{schema}

Return ONLY valid JSON with corrected plan.

JSON:";

        var request = new LlmRequest
        {
            Messages = new List<LlmMessage>
            {
                new(ChatRoles.User, prompt)
            }
        };

        var response = await _llmClient.CompleteAsync(request, ct);
        return ExtractJson(response.Content ?? "{}");
    }

    private static string ExtractJson(string content)
    {
        // Extract JSON from potential markdown code blocks
        var trimmed = content.Trim();
        if (trimmed.StartsWith("```"))
        {
            var lines = trimmed.Split('\n');
            var jsonLines = lines.Skip(1).TakeWhile(l => !l.StartsWith("```"));
            trimmed = string.Join("\n", jsonLines);
        }
        return trimmed;
    }

    private static (bool IsValid, bool Retryable, string? ErrorCode, string? ErrorMessage) ParseValidation(string result)
    {
        try
        {
            using var doc = JsonDocument.Parse(result);
            var validation = doc.RootElement.GetProperty("validation");
            var isValid = validation.GetProperty("isValid").GetBoolean();
            var retryable = validation.TryGetProperty("retryable", out var r) && r.GetBoolean();
            var errorCode = validation.TryGetProperty("errorCode", out var c) ? c.GetString() : null;
            var errorMessage = validation.TryGetProperty("errorMessage", out var e) ? e.GetString() : null;
            return (isValid, retryable, errorCode, errorMessage);
        }
        catch
        {
            return (false, false, "PARSE_ERROR", "Failed to parse validation result");
        }
    }

    private static List<QueryResultSet> ParseQueryResults(string executeResult)
    {
        var results = new List<QueryResultSet>();
        try
        {
            using var doc = JsonDocument.Parse(executeResult);
            
            // Parse meta
            DateTime generatedAt = DateTime.UtcNow;
            bool truncated = false;
            var warnings = new List<string>();

            if (doc.RootElement.TryGetProperty("meta", out var meta))
            {
                if (meta.TryGetProperty("truncated", out var t))
                    truncated = t.GetBoolean();
                
                // PATCH 30.01: Parse meta.freshness.asOfUtc (preferred) or legacy meta.generatedAtUtc
                if (meta.TryGetProperty("freshness", out var freshness) && 
                    freshness.TryGetProperty("asOfUtc", out var asOf))
                {
                    DateTime.TryParse(asOf.GetString(), out generatedAt);
                }
                else if (meta.TryGetProperty("generatedAtUtc", out var g))
                {
                    DateTime.TryParse(g.GetString(), out generatedAt);
                }
            }

            if (doc.RootElement.TryGetProperty("warnings", out var w) && w.ValueKind == JsonValueKind.Array)
            {
                foreach (var warning in w.EnumerateArray())
                {
                    if (warning.TryGetProperty("warning", out var wt))
                        warnings.Add(wt.GetString() ?? "");
                }
            }

            // Parse columns
            var columns = new List<string>();
            if (doc.RootElement.TryGetProperty("columns", out var cols) && cols.ValueKind == JsonValueKind.Array)
            {
                foreach (var col in cols.EnumerateArray())
                {
                    var name = col.TryGetProperty("name", out var n) ? n.GetString() : "";
                    columns.Add(name ?? "");
                }
            }

            // Parse rows (from actual result set - SP returns data rows separately)
            var rows = new List<List<object?>>();
            if (doc.RootElement.TryGetProperty("rows", out var rowsEl) && rowsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var row in rowsEl.EnumerateArray())
                {
                    var rowData = new List<object?>();
                    if (row.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var col in columns)
                        {
                            if (row.TryGetProperty(col, out var val))
                            {
                                rowData.Add(val.ValueKind switch
                                {
                                    JsonValueKind.Number => val.TryGetInt64(out var l) ? l : val.GetDecimal(),
                                    JsonValueKind.String => val.GetString(),
                                    _ => val.ToString()
                                });
                            }
                            else
                            {
                                rowData.Add(null);
                            }
                        }
                    }
                    rows.Add(rowData);
                }
            }

            // Create result set
            results.Add(new QueryResultSet
            {
                Label = "Results",
                Type = rows.Count > 1 ? QueryResultType.Breakdown : QueryResultType.Total,
                Columns = columns,
                Rows = rows,
                RowCount = rows.Count,
                Truncated = truncated,
                GeneratedAtUtc = generatedAt,
                Warnings = warnings
            });
        }
        catch
        {
            // Parse failed, return empty
        }

        return results;
    }

    private static TaskFrame BuildTaskFrame(string query, AnalyticsIntentResult? intent)
    {
        var taskFrame = new TaskFrame
        {
            TaskType = TaskType.Analytics,
            Confidence = intent?.Confidence ?? 0
        };

        // Extract entity from hints
        var entityHint = intent?.Hints
            .FirstOrDefault(h => h.StartsWith("entity:"));
        if (entityHint != null)
        {
            taskFrame.Entity = entityHint["entity:".Length..];
        }

        // Extract season filter
        var seasonHint = intent?.Hints
            .FirstOrDefault(h => h.StartsWith("season:"));
        if (seasonHint != null)
        {
            taskFrame.Filters.Add(new FilterSpec
            {
                FieldHint = "seasonCode",
                Op = "eq",
                Value = seasonHint["season:".Length..]
            });
        }

        return taskFrame;
    }

    private static AnalyticsOrchestratorResult CreateLimitExceeded(List<string> sequence, long durationMs)
    {
        return new AnalyticsOrchestratorResult
        {
            Success = false,
            ErrorMessage = "Tool call limit exceeded",
            ToolSequence = sequence,
            DurationMs = durationMs
        };
    }

    private static AnalyticsOrchestratorResult CreateNoDatasetFound(List<string> sequence, long durationMs, string language)
    {
        return new AnalyticsOrchestratorResult
        {
            Success = false,
            ErrorMessage = language == "vi" 
                ? "Không tìm thấy dataset phù hợp." 
                : "No matching dataset found.",
            ToolSequence = sequence,
            DurationMs = durationMs
        };
    }

    private static AnalyticsOrchestratorResult CreateValidationFailed(string? error, List<string> sequence, long durationMs, string language)
    {
        return new AnalyticsOrchestratorResult
        {
            Success = false,
            ErrorMessage = language == "vi"
                ? $"Không thể tạo kế hoạch truy vấn hợp lệ: {error}"
                : $"Unable to create valid query plan: {error}",
            ToolSequence = sequence,
            DurationMs = durationMs
        };
    }

    /// <summary>
    /// PATCH 29.09: Compute SHA256 hash of plan for logging (truncated to 8 chars).
    /// </summary>
    private static string ComputePlanHash(string planJson)
    {
        if (string.IsNullOrEmpty(planJson))
            return "empty";
            
        var bytes = System.Text.Encoding.UTF8.GetBytes(planJson);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hash)[..8].ToLowerInvariant();
    }

    /// <summary>
    /// PATCH 30.04: Quick catalog_search for borderline intent tie-breaking.
    /// Returns whether any datasets match the entity hint.
    /// </summary>
    public async Task<CatalogSearchResult> TryCatalogSearchAsync(
        string entityHint, 
        TilsoftExecutionContext context, 
        CancellationToken ct)
    {
        try
        {
            var tools = await _toolCatalogResolver.GetResolvedToolsAsync(ct);
            var toolLookup = tools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
            
            if (!toolLookup.TryGetValue(ToolCatalogSearch, out var tool))
            {
                _logger.LogWarning("catalog_search tool not available for tie-breaker");
                return new CatalogSearchResult { HasResults = false };
            }
            
            var searchArgs = JsonSerializer.Serialize(new { query = entityHint, topK = 3 });
            var searchResult = await ExecuteToolAsync(ToolCatalogSearch, searchArgs, toolLookup, context, ct);
            
            if (string.IsNullOrEmpty(searchResult))
                return new CatalogSearchResult { HasResults = false };
            
            using var doc = JsonDocument.Parse(searchResult);
            if (doc.RootElement.TryGetProperty("datasets", out var datasets) && 
                datasets.ValueKind == JsonValueKind.Array &&
                datasets.GetArrayLength() > 0)
            {
                return new CatalogSearchResult { HasResults = true };
            }

            return new CatalogSearchResult { HasResults = false };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Catalog search tie-breaker failed for entity: {Entity}", entityHint);
            return new CatalogSearchResult { HasResults = false };
        }
    }
}

/// <summary>
/// PATCH 30.04: Result of catalog search for intent tie-breaking.
/// </summary>
public sealed class CatalogSearchResult
{
    public bool HasResults { get; set; }
}

/// <summary>
/// Result of analytics orchestration.
/// </summary>
public sealed class AnalyticsOrchestratorResult
{
    public bool Success { get; set; }
    public string? Content { get; set; }
    public string? ErrorMessage { get; set; }
    public int ToolCallCount { get; set; }
    public List<string> ToolSequence { get; set; } = new();
    public long DurationMs { get; set; }
}

