using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Domain.Secrets;
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
    private readonly IExternalConnectionCatalog? _connectionCatalog;
    private readonly ISecretProvider? _secretProvider;

    public RestJsonToolAdapter(
        HttpClient httpClient,
        IExternalConnectionCatalog? connectionCatalog = null,
        ISecretProvider? secretProvider = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _connectionCatalog = connectionCatalog;
        _secretProvider = secretProvider;
    }

    public string AdapterType => Type;

    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!string.Equals(request.Operation, ToolAdapterOperationNames.ExecuteHttpJson, StringComparison.OrdinalIgnoreCase))
        {
            return ToolExecutionResult.Fail("REST_OPERATION_NOT_SUPPORTED", new { request.Operation });
        }

        var metadataResult = await BuildExecutionMetadataAsync(request, ct);
        if (!metadataResult.Success)
        {
            return ToolExecutionResult.Fail(metadataResult.ErrorCode!, metadataResult.Detail);
        }

        var executionMetadata = metadataResult.Metadata!;

        var method = ReadMetadata(executionMetadata.Values, "method") ?? "GET";
        var endpoint = ReadMetadata(executionMetadata.Values, "endpoint");
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
            uri = BuildUri(ReadMetadata(executionMetadata.Values, "baseUrl"), endpoint, request.ArgumentsJson, method);
        }
        catch (Exception ex) when (ex is UriFormatException or InvalidOperationException or JsonException)
        {
            return ToolExecutionResult.Fail("REST_BINDING_INVALID", new
            {
                reason = ex.Message,
                request.CapabilityKey
            });
        }

        var retryCount = ReadBoundedInt(executionMetadata.Values, "retryCount", DefaultRetryCount, 0, MaxRetryCount);
        var retryDelayMs = ReadBoundedInt(executionMetadata.Values, "retryDelayMs", DefaultRetryDelayMs, 0, MaxRetryDelayMs);
        using var timeoutCts = CreateTimeoutTokenSource(executionMetadata.Values, ct);
        var executionToken = timeoutCts?.Token ?? ct;

        for (var attempt = 1; attempt <= retryCount + 1; attempt++)
        {
            try
            {
                using var message = CreateMessage(request, method, uri, executionMetadata);
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

    private static HttpRequestMessage CreateMessage(
        ToolExecutionRequest request,
        string method,
        Uri uri,
        RestExecutionMetadata metadata)
    {
        var message = new HttpRequestMessage(new HttpMethod(method), uri);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        message.Headers.TryAddWithoutValidation("X-TILSOFTAI-Tenant", request.TenantId);
        message.Headers.TryAddWithoutValidation("X-TILSOFTAI-Correlation", request.CorrelationId);
        ApplyConfiguredHeaders(message, metadata.Values);
        ApplySecretHeaders(message, metadata.SecretHeaders);
        ApplyAuthPolicy(message, metadata);

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

    private static void ApplySecretHeaders(HttpRequestMessage message, IReadOnlyDictionary<string, string> headers)
    {
        foreach (var (headerName, value) in headers)
        {
            message.Headers.TryAddWithoutValidation(headerName, value);
        }
    }

    private static void ApplyAuthPolicy(HttpRequestMessage message, RestExecutionMetadata metadata)
    {
        if (metadata.Values.TryGetValue("authScheme", out var scheme)
            && !string.IsNullOrWhiteSpace(scheme)
            && !string.IsNullOrWhiteSpace(metadata.AuthToken))
        {
            message.Headers.Authorization = new AuthenticationHeaderValue(scheme, metadata.AuthToken);
        }

        if (metadata.Values.TryGetValue("apiKeyHeader", out var headerName)
            && !string.IsNullOrWhiteSpace(headerName)
            && !string.IsNullOrWhiteSpace(metadata.ApiKey))
        {
            message.Headers.TryAddWithoutValidation(headerName, metadata.ApiKey);
        }
    }

    private static string? ReadMetadata(ToolExecutionRequest request, string key)
    {
        return ReadMetadata(request.Metadata, key);
    }

    private static string? ReadMetadata(IReadOnlyDictionary<string, string?> metadata, string key)
    {
        return metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }

    private static int ReadBoundedInt(IReadOnlyDictionary<string, string?> metadata, string key, int fallback, int min, int max)
    {
        var raw = ReadMetadata(metadata, key);
        if (!int.TryParse(raw, out var value))
        {
            return fallback;
        }

        return Math.Clamp(value, min, max);
    }

    private static CancellationTokenSource? CreateTimeoutTokenSource(IReadOnlyDictionary<string, string?> metadata, CancellationToken ct)
    {
        var timeoutSeconds = ReadBoundedInt(metadata, "timeoutSeconds", 0, 0, 300);
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

    private async Task<RestExecutionMetadataResult> BuildExecutionMetadataAsync(
        ToolExecutionRequest request,
        CancellationToken ct)
    {
        var values = new Dictionary<string, string?>(request.Metadata, StringComparer.OrdinalIgnoreCase);

        if (HasValue(values, "authToken") || HasValue(values, "apiKey"))
        {
            return RestExecutionMetadataResult.Fail("REST_SECRET_POLICY_VIOLATION", new
            {
                reason = "raw_secret_metadata_not_allowed",
                request.CapabilityKey
            });
        }

        var connectionName = ReadMetadata(values, "connectionName");
        if (!string.IsNullOrWhiteSpace(connectionName))
        {
            if (_connectionCatalog is null)
            {
                return RestExecutionMetadataResult.Fail("REST_CONNECTION_CATALOG_UNAVAILABLE", new
                {
                    connectionName,
                    request.CapabilityKey
                });
            }

            var connection = _connectionCatalog.Resolve(connectionName);
            if (connection is null)
            {
                return RestExecutionMetadataResult.Fail("REST_CONNECTION_NOT_FOUND", new
                {
                    connectionName,
                    request.CapabilityKey
                });
            }

            ApplyConnection(values, connection);
        }

        var secretHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, secretKey) in values
            .Where(pair => pair.Key.StartsWith("headerSecret:", StringComparison.OrdinalIgnoreCase))
            .ToArray())
        {
            var headerName = key["headerSecret:".Length..].Trim();
            if (string.IsNullOrWhiteSpace(headerName) || string.IsNullOrWhiteSpace(secretKey))
            {
                continue;
            }

            var secret = await ResolveSecretAsync(secretKey!, request.CapabilityKey, ct);
            if (!secret.Success)
            {
                return RestExecutionMetadataResult.Fail(secret.ErrorCode!, secret.Detail!);
            }

            secretHeaders[headerName] = secret.Value!;
        }

        string? authToken = null;
        var authTokenSecret = ReadMetadata(values, "authTokenSecret");
        if (!string.IsNullOrWhiteSpace(authTokenSecret))
        {
            var secret = await ResolveSecretAsync(authTokenSecret, request.CapabilityKey, ct);
            if (!secret.Success)
            {
                return RestExecutionMetadataResult.Fail(secret.ErrorCode!, secret.Detail!);
            }

            authToken = secret.Value;
        }

        string? apiKey = null;
        var apiKeySecret = ReadMetadata(values, "apiKeySecret");
        if (!string.IsNullOrWhiteSpace(apiKeySecret))
        {
            var secret = await ResolveSecretAsync(apiKeySecret, request.CapabilityKey, ct);
            if (!secret.Success)
            {
                return RestExecutionMetadataResult.Fail(secret.ErrorCode!, secret.Detail!);
            }

            apiKey = secret.Value;
        }

        return RestExecutionMetadataResult.Ok(new RestExecutionMetadata(values, authToken, apiKey, secretHeaders));
    }

    private async Task<SecretResolutionResult> ResolveSecretAsync(
        string secretKey,
        string capabilityKey,
        CancellationToken ct)
    {
        if (_secretProvider is null)
        {
            return SecretResolutionResult.Fail("REST_SECRET_PROVIDER_UNAVAILABLE", new
            {
                secretKey,
                capabilityKey
            });
        }

        var value = await _secretProvider.GetSecretAsync(secretKey, ct);
        return string.IsNullOrWhiteSpace(value)
            ? SecretResolutionResult.Fail("REST_SECRET_NOT_FOUND", new { secretKey, capabilityKey })
            : SecretResolutionResult.Ok(value);
    }

    private static void ApplyConnection(Dictionary<string, string?> values, ExternalConnectionOptions connection)
    {
        SetIfMissing(values, "baseUrl", connection.BaseUrl);
        SetIfMissing(values, "authScheme", connection.AuthScheme);
        SetIfMissing(values, "authTokenSecret", connection.AuthTokenSecret);
        SetIfMissing(values, "apiKeyHeader", connection.ApiKeyHeader);
        SetIfMissing(values, "apiKeySecret", connection.ApiKeySecret);
        SetIfMissing(values, "timeoutSeconds", connection.TimeoutSeconds > 0 ? connection.TimeoutSeconds.ToString() : null);
        SetIfMissing(values, "retryCount", connection.RetryCount > 0 ? connection.RetryCount.ToString() : null);
        SetIfMissing(values, "retryDelayMs", connection.RetryDelayMs > 0 ? connection.RetryDelayMs.ToString() : null);

        foreach (var (header, value) in connection.Headers)
        {
            SetIfMissing(values, $"header:{header}", value);
        }

        foreach (var (header, secretKey) in connection.HeaderSecrets)
        {
            SetIfMissing(values, $"headerSecret:{header}", secretKey);
        }
    }

    private static void SetIfMissing(Dictionary<string, string?> values, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || HasValue(values, key))
        {
            return;
        }

        values[key] = value;
    }

    private static bool HasValue(IReadOnlyDictionary<string, string?> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value);

    private sealed record RestExecutionMetadata(
        IReadOnlyDictionary<string, string?> Values,
        string? AuthToken,
        string? ApiKey,
        IReadOnlyDictionary<string, string> SecretHeaders);

    private sealed class RestExecutionMetadataResult
    {
        private RestExecutionMetadataResult(
            RestExecutionMetadata? metadata,
            string? errorCode,
            object? detail)
        {
            Metadata = metadata;
            ErrorCode = errorCode;
            Detail = detail;
        }

        public bool Success => ErrorCode is null;
        public RestExecutionMetadata? Metadata { get; }
        public string? ErrorCode { get; }
        public object? Detail { get; }

        public static RestExecutionMetadataResult Ok(RestExecutionMetadata metadata) => new(metadata, null, null);
        public static RestExecutionMetadataResult Fail(string errorCode, object detail) => new(null, errorCode, detail);
    }

    private sealed class SecretResolutionResult
    {
        private SecretResolutionResult(string? value, string? errorCode, object? detail)
        {
            Value = value;
            ErrorCode = errorCode;
            Detail = detail;
        }

        public bool Success => ErrorCode is null;
        public string? Value { get; }
        public string? ErrorCode { get; }
        public object? Detail { get; }

        public static SecretResolutionResult Ok(string value) => new(value, null, null);
        public static SecretResolutionResult Fail(string errorCode, object detail) => new(null, errorCode, detail);
    }
}
