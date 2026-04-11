using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TILSOFTAI.Tools.Abstractions;

namespace TILSOFTAI.Infrastructure.Http;

public sealed class RestJsonToolAdapter : IToolAdapter
{
    public const string Type = "rest-json";
    private const int DefaultRetryCount = 0;
    private const int DefaultRetryDelayMs = 100;
    private const int MaxRetryCount = 3;
    private const int MaxRetryDelayMs = 5_000;

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

        var method = ReadMetadata(request, "method") ?? "GET";
        var endpoint = ReadMetadata(request, "endpoint");
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return ToolExecutionResult.Fail("REST_BINDING_INVALID", new
            {
                reason = "missing_endpoint",
                request.CapabilityKey
            });
        }

        Uri uri;
        try
        {
            uri = BuildUri(ReadMetadata(request, "baseUrl"), endpoint, request.ArgumentsJson, method);
        }
        catch (Exception ex) when (ex is UriFormatException or InvalidOperationException or JsonException)
        {
            return ToolExecutionResult.Fail("REST_BINDING_INVALID", new
            {
                reason = ex.Message,
                request.CapabilityKey
            });
        }

        var retryCount = ReadBoundedInt(request, "retryCount", DefaultRetryCount, 0, MaxRetryCount);
        var retryDelayMs = ReadBoundedInt(request, "retryDelayMs", DefaultRetryDelayMs, 0, MaxRetryDelayMs);
        using var timeoutCts = CreateTimeoutTokenSource(request, ct);
        var executionToken = timeoutCts?.Token ?? ct;

        for (var attempt = 1; attempt <= retryCount + 1; attempt++)
        {
            try
            {
                using var message = CreateMessage(request, method, uri);
                using var response = await _httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, executionToken);
                var payload = await response.Content.ReadAsStringAsync(executionToken);

                if (response.IsSuccessStatusCode)
                {
                    return ToolExecutionResult.Ok(payload, payload);
                }

                if (IsTransient(response.StatusCode) && attempt <= retryCount)
                {
                    await DelayBeforeRetryAsync(retryDelayMs, attempt, executionToken);
                    continue;
                }

                return ToolExecutionResult.Fail(
                    ClassifyStatusCode(response.StatusCode),
                    new
                    {
                        statusCode = (int)response.StatusCode,
                        reason = response.ReasonPhrase,
                        body = payload,
                        attempts = attempt
                    });
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return ToolExecutionResult.Fail("REST_TIMEOUT", new
                {
                    timeoutSeconds = ReadMetadata(request, "timeoutSeconds"),
                    attempts = attempt
                });
            }
            catch (HttpRequestException) when (attempt <= retryCount)
            {
                await DelayBeforeRetryAsync(retryDelayMs, attempt, executionToken);
                continue;
            }
            catch (HttpRequestException ex)
            {
                return ToolExecutionResult.Fail("REST_TRANSPORT_ERROR", new
                {
                    message = ex.Message,
                    attempts = attempt
                });
            }
        }

        return ToolExecutionResult.Fail("REST_TRANSPORT_ERROR", new { attempts = retryCount + 1 });
    }

    private static HttpRequestMessage CreateMessage(ToolExecutionRequest request, string method, Uri uri)
    {
        var message = new HttpRequestMessage(new HttpMethod(method), uri);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        message.Headers.TryAddWithoutValidation("X-TILSOFTAI-Tenant", request.TenantId);
        message.Headers.TryAddWithoutValidation("X-TILSOFTAI-Correlation", request.CorrelationId);
        ApplyConfiguredHeaders(message, request.Metadata);
        ApplyAuthPolicy(message, request.Metadata);

        if (ShouldSendBody(method))
        {
            message.Content = new StringContent(
                string.IsNullOrWhiteSpace(request.ArgumentsJson) ? "{}" : request.ArgumentsJson,
                Encoding.UTF8,
                "application/json");
        }

        return message;
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

    private static void ApplyConfiguredHeaders(HttpRequestMessage message, IReadOnlyDictionary<string, string?> metadata)
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

    private static void ApplyAuthPolicy(HttpRequestMessage message, IReadOnlyDictionary<string, string?> metadata)
    {
        if (metadata.TryGetValue("authScheme", out var scheme)
            && metadata.TryGetValue("authToken", out var token)
            && !string.IsNullOrWhiteSpace(scheme)
            && !string.IsNullOrWhiteSpace(token))
        {
            message.Headers.Authorization = new AuthenticationHeaderValue(scheme, token);
        }

        if (metadata.TryGetValue("apiKeyHeader", out var headerName)
            && metadata.TryGetValue("apiKey", out var apiKey)
            && !string.IsNullOrWhiteSpace(headerName)
            && !string.IsNullOrWhiteSpace(apiKey))
        {
            message.Headers.TryAddWithoutValidation(headerName, apiKey);
        }
    }

    private static string? ReadMetadata(ToolExecutionRequest request, string key)
    {
        return request.Metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }

    private static int ReadBoundedInt(ToolExecutionRequest request, string key, int fallback, int min, int max)
    {
        var raw = ReadMetadata(request, key);
        if (!int.TryParse(raw, out var value))
        {
            return fallback;
        }

        return Math.Clamp(value, min, max);
    }

    private static CancellationTokenSource? CreateTimeoutTokenSource(ToolExecutionRequest request, CancellationToken ct)
    {
        var timeoutSeconds = ReadBoundedInt(request, "timeoutSeconds", 0, 0, 300);
        if (timeoutSeconds <= 0)
        {
            return null;
        }

        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        return cts;
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

    private static bool IsTransient(System.Net.HttpStatusCode statusCode) =>
        statusCode is System.Net.HttpStatusCode.RequestTimeout
            or (System.Net.HttpStatusCode)429
            or System.Net.HttpStatusCode.BadGateway
            or System.Net.HttpStatusCode.ServiceUnavailable
            or System.Net.HttpStatusCode.GatewayTimeout
        || (int)statusCode >= 500;

    private static string ClassifyStatusCode(System.Net.HttpStatusCode statusCode)
    {
        var numeric = (int)statusCode;
        if (statusCode is System.Net.HttpStatusCode.RequestTimeout or (System.Net.HttpStatusCode)429)
        {
            return "REST_TRANSIENT_HTTP_ERROR";
        }

        if (numeric >= 500)
        {
            return "REST_SERVER_ERROR";
        }

        return numeric >= 400
            ? "REST_CLIENT_ERROR"
            : "REST_HTTP_ERROR";
    }

    private static Task DelayBeforeRetryAsync(int retryDelayMs, int attempt, CancellationToken ct)
    {
        if (retryDelayMs <= 0)
        {
            return Task.CompletedTask;
        }

        var delay = Math.Min(retryDelayMs * attempt, MaxRetryDelayMs);
        return Task.Delay(delay, ct);
    }
}
