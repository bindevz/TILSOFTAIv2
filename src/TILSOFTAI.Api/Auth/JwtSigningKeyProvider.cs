using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Tokens;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Api.Auth;

public sealed class JwtSigningKeyProvider : IJwtSigningKeyProvider
{
    private static readonly ActivitySource ActivitySource = new("TILSOFTAI.Auth");
    private const string JwksHttpClientName = "jwks";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AuthOptions _authOptions;
    private readonly OpenTelemetryOptions _telemetryOptions;
    private readonly ILogger<JwtSigningKeyProvider> _logger;
    private readonly ConfigurationManager<JsonWebKeySet>? _configurationManager;
    private volatile IReadOnlyCollection<SecurityKey> _keys = Array.Empty<SecurityKey>();
    private string? _lastError;

    public JwtSigningKeyProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<AuthOptions> authOptions,
        IOptions<OpenTelemetryOptions> telemetryOptions,
        ILogger<JwtSigningKeyProvider> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _authOptions = authOptions?.Value ?? throw new ArgumentNullException(nameof(authOptions));
        _telemetryOptions = telemetryOptions?.Value ?? new OpenTelemetryOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (!string.IsNullOrWhiteSpace(_authOptions.JwksUrl))
        {
            var client = _httpClientFactory.CreateClient(JwksHttpClientName);
            _configurationManager = new ConfigurationManager<JsonWebKeySet>(
                _authOptions.JwksUrl,
                new JsonWebKeySetRetriever(),
                client)
            {
                AutomaticRefreshInterval = TimeSpan.FromMinutes(Math.Max(1, _authOptions.JwksRefreshIntervalMinutes)),
                RefreshInterval = TimeSpan.FromMinutes(Math.Max(1, _authOptions.JwksRefreshIntervalMinutes))
            };
        }
    }

    public IReadOnlyCollection<SecurityKey> GetKeys() => _keys;

    public async Task<bool> RefreshAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_authOptions.JwksUrl))
        {
            _lastError = "JWKS URL is not configured.";
            _logger.LogWarning("JWT signing key refresh skipped because JWKS URL is empty.");
            return false;
        }

        using var activity = _telemetryOptions.EnableAuthKeyRefreshTracing
            ? ActivitySource.StartActivity("jwks.refresh")
            : null;

        try
        {
            JsonWebKeySet? jwks;
            if (_configurationManager is not null)
            {
                jwks = await _configurationManager.GetConfigurationAsync(ct);
            }
            else
            {
                var client = _httpClientFactory.CreateClient(JwksHttpClientName);
                jwks = await client.GetFromJsonAsync<JsonWebKeySet>(_authOptions.JwksUrl, ct);
            }

            if (jwks?.Keys == null || jwks.Keys.Count == 0)
            {
                _lastError = "JWKS response contained no keys.";
                _logger.LogWarning("JWT signing key refresh returned no keys.");
                activity?.SetTag("jwks.refresh.success", false);
                return false;
            }

            _keys = jwks.Keys.Cast<SecurityKey>().ToArray();
            _lastError = null;
            activity?.SetTag("jwks.refresh.success", true);
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            _lastError = ex.Message;
            _logger.LogWarning(ex, "JWT signing key refresh failed. Using last-known-good keys.");
            activity?.SetTag("jwks.refresh.success", false);
            return false;
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            _logger.LogError(ex, "Unexpected error refreshing JWT signing keys. Using last-known-good keys.");
            activity?.SetTag("jwks.refresh.success", false);
            return false;
        }
    }
}
