using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Orchestration.Caching;

namespace TILSOFTAI.Infrastructure.Llm;

public sealed class OpenAiEmbeddingClient : IEmbeddingClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly LlmOptions _llmOptions;
    private readonly SemanticCacheOptions _cacheOptions;
    private readonly ILogger<OpenAiEmbeddingClient> _logger;

    public OpenAiEmbeddingClient(
        HttpClient httpClient, 
        IOptions<LlmOptions> llmOptions,
        IOptions<SemanticCacheOptions> cacheOptions,
        ILogger<OpenAiEmbeddingClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _llmOptions = llmOptions?.Value ?? throw new ArgumentNullException(nameof(llmOptions));
        _cacheOptions = cacheOptions?.Value ?? throw new ArgumentNullException(nameof(cacheOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (_llmOptions.TimeoutSeconds > 0)
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(_llmOptions.TimeoutSeconds);
        }
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<float>();
        }

        var endpoint = ResolveEmbeddingEndpoint(_llmOptions.Endpoint);
        var model = _cacheOptions.EmbeddingModel; // e.g. "text-embedding-3-small"

        var requestPayload = new
        {
            model = model,
            input = text
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _llmOptions.ApiKey);
        httpRequest.Content = new StringContent( JsonSerializer.Serialize(requestPayload, JsonOptions) );
        httpRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
             var body = await response.Content.ReadAsStringAsync(cancellationToken);
             _logger.LogError("Embedding request failed. Status={Status} Body={Body}", response.StatusCode, body);
             throw new InvalidOperationException($"Embedding request failed: {response.StatusCode}");
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        
        // Response format: { "data": [ { "embedding": [ ... ] } ] }
        var dataArray = doc.RootElement.GetProperty("data");
        if (dataArray.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("Embedding response contained no data.");
        }

        var embeddingJson = dataArray[0].GetProperty("embedding");
        var floats = new float[embeddingJson.GetArrayLength()];
        var i = 0;
        foreach (var item in embeddingJson.EnumerateArray())
        {
            floats[i++] = item.GetSingle();
        }

        return floats;
    }

    private static string ResolveEmbeddingEndpoint(string chatEndpoint)
    {
        if (string.IsNullOrWhiteSpace(chatEndpoint))
        {
            return "https://api.openai.com/v1/embeddings";
        }

        if (chatEndpoint.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            return chatEndpoint[..^17] + "/embeddings";
        }

        // If it's just a base URL (e.g. "https://api.openai.com/v1"), append /embeddings
        if (chatEndpoint.EndsWith("/v1", StringComparison.OrdinalIgnoreCase) || chatEndpoint.EndsWith("/v1/", StringComparison.OrdinalIgnoreCase))
        {
            var clean = chatEndpoint.TrimEnd('/');
            return $"{clean}/embeddings";
        }

        // Fallback or assume user provided embedding endpoint specifically? 
        // Spec says "uses existing LlmOptions endpoint". 
        // Let's assume standard OpenAI structure if not explicitly Chat Completions.
        return "https://api.openai.com/v1/embeddings";
    }
}
