using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TILSOFTAI.Tools.Abstractions;

namespace TILSOFTAI.Infrastructure.Http;

public sealed class RestJsonToolAdapter : IToolAdapter
{
    public const string Type = "rest-json";

    private readonly HttpClient _httpClient;

    public RestJsonToolAdapter(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public string AdapterType => Type;

    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!string.Equals(request.Operation, ToolAdapterOperationNames.ExecuteHttpJson, StringComparison.OrdinalIgnoreCase))
        {
            return ToolExecutionResult.Fail("REST_OPERATION_NOT_SUPPORTED", new { request.Operation });
        }

        var method = ReadMetadata(request, "method", required: false) ?? "GET";
        var endpoint = ReadMetadata(request, "endpoint", required: true)!;
        var baseUrl = ReadMetadata(request, "baseUrl", required: false);
        var uri = BuildUri(baseUrl, endpoint, request.ArgumentsJson, method);

        using var message = new HttpRequestMessage(new HttpMethod(method), uri);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        message.Headers.TryAddWithoutValidation("X-TILSOFTAI-Tenant", request.TenantId);
        message.Headers.TryAddWithoutValidation("X-TILSOFTAI-Correlation", request.CorrelationId);
        ApplyConfiguredHeaders(message, request.Metadata);

        if (ShouldSendBody(method))
        {
            message.Content = new StringContent(
                string.IsNullOrWhiteSpace(request.ArgumentsJson) ? "{}" : request.ArgumentsJson,
                Encoding.UTF8,
                "application/json");
        }

        using var response = await _httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            return ToolExecutionResult.Fail(
                "REST_HTTP_ERROR",
                new
                {
                    statusCode = (int)response.StatusCode,
                    reason = response.ReasonPhrase,
                    body = payload
                });
        }

        return ToolExecutionResult.Ok(payload, payload);
    }

    private static Uri BuildUri(string? baseUrl, string endpoint, string argumentsJson, string method)
    {
        var endpointUri = Uri.TryCreate(endpoint, UriKind.Absolute, out var absoluteEndpoint)
            ? absoluteEndpoint
            : null;

        var uri = endpointUri ?? new Uri(new Uri(RequireBaseUrl(baseUrl), UriKind.Absolute), endpoint);
        if (ShouldSendBody(method))
        {
            return uri;
        }

        var query = BuildQuery(argumentsJson);
        if (string.IsNullOrEmpty(query))
        {
            return uri;
        }

        var separator = string.IsNullOrEmpty(uri.Query) ? "?" : "&";
        return new Uri(uri + separator + query);
    }

    private static string BuildQuery(string argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return string.Empty;
        }

        using var document = JsonDocument.Parse(argumentsJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        var pairs = new List<string>();
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                continue;
            }

            var name = property.Name.TrimStart('@');
            var value = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString()
                : property.Value.GetRawText();

            if (string.IsNullOrWhiteSpace(name) || value is null)
            {
                continue;
            }

            pairs.Add($"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(value)}");
        }

        return string.Join("&", pairs);
    }

    private static void ApplyConfiguredHeaders(
        HttpRequestMessage message,
        IReadOnlyDictionary<string, string?> metadata)
    {
        foreach (var (key, value) in metadata)
        {
            if (!key.StartsWith("header:", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var headerName = key["header:".Length..].Trim();
            if (!string.IsNullOrWhiteSpace(headerName))
            {
                message.Headers.TryAddWithoutValidation(headerName, value);
            }
        }
    }

    private static string? ReadMetadata(ToolExecutionRequest request, string key, bool required)
    {
        if (request.Metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (required)
        {
            throw new InvalidOperationException($"REST JSON tool adapter requires metadata value '{key}'.");
        }

        return null;
    }

    private static string RequireBaseUrl(string? baseUrl)
    {
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            return baseUrl;
        }

        throw new InvalidOperationException("REST JSON tool adapter requires 'baseUrl' for relative endpoints.");
    }

    private static bool ShouldSendBody(string method) =>
        string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase)
        || string.Equals(method, "PUT", StringComparison.OrdinalIgnoreCase)
        || string.Equals(method, "PATCH", StringComparison.OrdinalIgnoreCase);
}
