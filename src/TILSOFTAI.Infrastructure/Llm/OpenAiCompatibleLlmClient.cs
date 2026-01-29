using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Orchestration.Llm;
using TILSOFTAI.Orchestration.Tools;

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

    public OpenAiCompatibleLlmClient(HttpClient httpClient, IOptions<LlmOptions> options, ILogger<OpenAiCompatibleLlmClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (_options.TimeoutSeconds > 0)
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        }
    }

    public async Task<LlmResponse> CompleteAsync(LlmRequest req, CancellationToken ct)
    {
        var requestPayload = BuildRequest(req, stream: false);
        using var httpRequest = BuildHttpRequest(requestPayload);

        var sw = Stopwatch.StartNew();
        using var response = await _httpClient.SendAsync(httpRequest, ct);
        sw.Stop();

        LogResponse(response.StatusCode, sw.Elapsed);

        if (!response.IsSuccessStatusCode)
        {
            var body = await ReadErrorBodyAsync(response, ct);
            throw new InvalidOperationException($"LLM request failed with status {(int)response.StatusCode}: {body}");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        return OpenAiResponseParser.ParseCompletion(json);
    }

    public async IAsyncEnumerable<LlmStreamEvent> StreamAsync(LlmRequest req, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var requestPayload = BuildRequest(req, stream: true);
        using var httpRequest = BuildHttpRequest(requestPayload);

        var sw = Stopwatch.StartNew();
        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
        sw.Stop();

        LogResponse(response.StatusCode, sw.Elapsed);

        if (!response.IsSuccessStatusCode)
        {
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

    private void LogResponse(System.Net.HttpStatusCode statusCode, TimeSpan elapsed)
    {
        var host = string.Empty;
        if (Uri.TryCreate(_options.Endpoint, UriKind.Absolute, out var uri))
        {
            host = uri.Host;
        }

        _logger.LogInformation("LLM request completed. Host={Host} Status={StatusCode} DurationMs={DurationMs}", host, (int)statusCode, elapsed.TotalMilliseconds);
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
}
