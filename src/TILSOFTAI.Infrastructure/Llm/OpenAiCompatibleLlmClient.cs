using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Orchestration.Llm;
using TILSOFTAI.Orchestration.Tools;
using TILSOFTAI.Infrastructure.Telemetry;
using TILSOFTAI.Domain.Metrics;

namespace TILSOFTAI.Infrastructure.Llm;

public sealed class OpenAiCompatibleLlmClient : ILlmClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly LlmOptions _options;
    private readonly ILogger<OpenAiCompatibleLlmClient> _logger;
    private readonly LlmInstrumentation _instrumentation;
    private readonly IMetricsService _metrics;
    private readonly TILSOFTAI.Domain.Resilience.IRetryPolicy _retryPolicy;

    public OpenAiCompatibleLlmClient(HttpClient httpClient, IOptions<LlmOptions> options, ILogger<OpenAiCompatibleLlmClient> logger, LlmInstrumentation instrumentation, IMetricsService metrics, TILSOFTAI.Infrastructure.Resilience.RetryPolicyRegistry retryRegistry)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _instrumentation = instrumentation ?? throw new ArgumentNullException(nameof(instrumentation));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _retryPolicy = retryRegistry.GetOrCreate("llm");

        if (_options.TimeoutSeconds > 0)
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        }
    }

    public async Task<LlmResponse> CompleteAsync(LlmRequest req, CancellationToken ct)
    {
        using var activity = _instrumentation.StartRequest(_options.Model);
        
        var requestPayload = BuildRequest(req, stream: false);
        using var httpRequest = BuildHttpRequest(requestPayload);

        var sw = Stopwatch.StartNew();
        using var timer = _metrics.CreateTimer(MetricNames.LlmRequestDurationSeconds, new Dictionary<string, string> { { "model", _options.Model }, { "streaming", "false" } });
        
        _metrics.IncrementCounter(MetricNames.LlmRequestsTotal, new Dictionary<string, string> { { "model", _options.Model }, { "streaming", "false" } });

        using var response = await _retryPolicy.ExecuteAsync<HttpResponseMessage>(async ct0 => await _httpClient.SendAsync(httpRequest, ct0), ct);
        sw.Stop();

        if (!response.IsSuccessStatusCode)
        {
            _metrics.IncrementCounter(MetricNames.ErrorsTotal, new Dictionary<string, string> { { "code", "LlmError" }, { "status", ((int)response.StatusCode).ToString() } });
            LogResponse(response.StatusCode, sw.Elapsed, req.Messages.Count, req.Tools.Count, isStreaming: false);
            var body = await ReadErrorBodyAsync(response, ct);
            throw new InvalidOperationException($"LLM request failed with status {(int)response.StatusCode}: {body}");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        var result = OpenAiResponseParser.ParseCompletion(json);

        if (result.Usage != null)
        {
            _instrumentation.RecordUsage(result.Usage.InputTokens, result.Usage.OutputTokens, result.Usage.TotalTokens);
            _metrics.IncrementCounter(MetricNames.LlmTokensTotal, new Dictionary<string, string> { { "model", _options.Model }, { "type", "prompt" } }, result.Usage.InputTokens);
            _metrics.IncrementCounter(MetricNames.LlmTokensTotal, new Dictionary<string, string> { { "model", _options.Model }, { "type", "completion" } }, result.Usage.OutputTokens);
        }

        LogResponse(response.StatusCode, sw.Elapsed, req.Messages.Count, req.Tools.Count, isStreaming: false, result.Usage);

        return result;
    }

    public async IAsyncEnumerable<LlmStreamEvent> StreamAsync(LlmRequest req, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        using var activity = _instrumentation.StartRequest(_options.Model);

        var requestPayload = BuildRequest(req, stream: true);
        using var httpRequest = BuildHttpRequest(requestPayload);

        var sw = Stopwatch.StartNew();
        using var timer = _metrics.CreateTimer(MetricNames.LlmRequestDurationSeconds, new Dictionary<string, string> { { "model", _options.Model }, { "streaming", "true" } });
        _metrics.IncrementCounter(MetricNames.LlmRequestsTotal, new Dictionary<string, string> { { "model", _options.Model }, { "streaming", "true" } });

        using var response = await _retryPolicy.ExecuteAsync<HttpResponseMessage>(async ct0 => 
            await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct0), ct);
        sw.Stop();

        LogResponse(response.StatusCode, sw.Elapsed, req.Messages.Count, req.Tools.Count, isStreaming: true);

        if (!response.IsSuccessStatusCode)
        {
            _metrics.IncrementCounter(MetricNames.ErrorsTotal, new Dictionary<string, string> { { "code", "LlmError" }, { "status", ((int)response.StatusCode).ToString() } });
            var body = await ReadErrorBodyAsync(response, ct);
            yield return LlmStreamEvent.ErrorEvent("LLM request failed.");
            throw new InvalidOperationException($"LLM request failed with status {(int)response.StatusCode}: {body}");
        }

        var state = new OpenAiStreamState();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync();
            if (line is null)
            {
                break;
            }

            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var data = line[5..].Trim();
            if (string.IsNullOrWhiteSpace(data))
            {
                continue;
            }

            if (string.Equals(data, "[DONE]", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            IEnumerable<LlmStreamEvent> events;
            bool malformed = false;
            try
            {
                events = OpenAiResponseParser.HandleStreamChunk(state, data);
            }
            catch (JsonException)
            {
                malformed = true;
                events = Array.Empty<LlmStreamEvent>();
            }

            if (malformed)
            {
                yield return LlmStreamEvent.ErrorEvent("Malformed streaming payload.");
                continue;
            }

            foreach (var evt in events)
            {
                yield return evt;
            }
        }

        foreach (var evt in OpenAiResponseParser.FinalizeStream(state))
        {
            yield return evt;
        }
    }

    private OpenAiChatRequest BuildRequest(LlmRequest request, bool stream)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(_options.Endpoint))
        {
            throw new InvalidOperationException("Llm:Endpoint is required for OpenAI-compatible provider.");
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("Llm:ApiKey is required for OpenAI-compatible provider.");
        }

        if (string.IsNullOrWhiteSpace(_options.Model))
        {
            throw new InvalidOperationException("Llm:Model is required for OpenAI-compatible provider.");
        }

        var messages = new List<OpenAiMessage>
        {
            new()
            {
                Role = "system",
                Content = request.SystemPrompt ?? string.Empty
            }
        };

        foreach (var message in request.Messages)
        {
            messages.Add(new OpenAiMessage
            {
                Role = message.Role,
                Content = message.Content,
                Name = message.Name
            });
        }

        var tools = new List<OpenAiTool>();
        foreach (var tool in request.Tools)
        {
            var parameters = ParseJsonSchema(tool.JsonSchema);
            tools.Add(new OpenAiTool
            {
                Type = "function",
                Function = new OpenAiFunction
                {
                    Name = tool.Name,
                    Description = BuildToolDescription(tool),
                    Parameters = parameters
                }
            });
        }

        var maxTokens = _options.MaxResponseTokens > 0
            ? _options.MaxResponseTokens
            : request.MaxTokens;

        if (request.MaxTokens > 0 && request.MaxTokens < maxTokens)
        {
            maxTokens = request.MaxTokens;
        }

        return new OpenAiChatRequest
        {
            Model = _options.Model,
            Messages = messages,
            Tools = tools.Count == 0 ? null : tools,
            ToolChoice = "auto",
            Temperature = _options.Temperature,
            MaxTokens = maxTokens > 0 ? maxTokens : null,
            Stream = stream
        };
    }

    private HttpRequestMessage BuildHttpRequest(OpenAiChatRequest payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions));
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return request;
    }

    private void LogResponse(System.Net.HttpStatusCode statusCode, TimeSpan elapsed, int? messageCount = null, int? toolCount = null, bool isStreaming = false, LlmUsage? usage = null)
    {
        var host = string.Empty;
        if (Uri.TryCreate(_options.Endpoint, UriKind.Absolute, out var uri))
        {
            host = uri.Host;
        }

        if ((int)statusCode >= 400)
        {
            _logger.LogWarning(
                "LLM request failed | Host: {Host} | Model: {Model} | Status: {StatusCode} | Duration: {DurationMs}ms | Streaming: {IsStreaming}",
                host, _options.Model, (int)statusCode, elapsed.TotalMilliseconds, isStreaming);
        }
        else
        {
            _logger.LogInformation(
                "LLM request completed | Host: {Host} | Model: {Model} | Status: {StatusCode} | Duration: {DurationMs}ms | Streaming: {IsStreaming} | Messages: {MessageCount} | Tools: {ToolCount} | InputTokens: {InputTokens} | OutputTokens: {OutputTokens}",
                host, _options.Model, (int)statusCode, elapsed.TotalMilliseconds, isStreaming, messageCount, toolCount, usage?.InputTokens, usage?.OutputTokens);
        }
    }

    private static string BuildToolDescription(ToolDefinition tool)
    {
        var description = tool.Description ?? string.Empty;
        var instruction = tool.Instruction ?? string.Empty;
        return string.IsNullOrWhiteSpace(description)
            ? $"INSTRUCTION:\n{instruction}"
            : $"{description}\nINSTRUCTION:\n{instruction}";
    }

    private static object ParseJsonSchema(string jsonSchema)
    {
        if (string.IsNullOrWhiteSpace(jsonSchema))
        {
            return new Dictionary<string, object>();
        }

        using var doc = JsonDocument.Parse(jsonSchema);
        return doc.RootElement.Clone();
    }

    private static async Task<string> ReadErrorBodyAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        if (body.Length > 32768)
        {
            body = body[..32768];
        }
        return body;
    }

    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to list models as a lightweight ping
            using var request = new HttpRequestMessage(HttpMethod.Get, "models");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
