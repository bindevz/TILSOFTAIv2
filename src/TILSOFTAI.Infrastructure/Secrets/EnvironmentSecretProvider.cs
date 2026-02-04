using TILSOFTAI.Domain.Secrets;

namespace TILSOFTAI.Infrastructure.Secrets;

/// <summary>
/// Secret provider that reads from environment variables.
/// </summary>
public sealed class EnvironmentSecretProvider : ISecretProvider
{
    public string ProviderName => "Environment";

    public Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        // Convert secret path to environment variable name
        // tilsoft/sql/connection-string -> TILSOFT_SQL_CONNECTION_STRING
        var envVarName = ConvertToEnvVarName(key);
        var value = Environment.GetEnvironmentVariable(envVarName);
        return Task.FromResult(value);
    }

    public Task<bool> SecretExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        var envVarName = ConvertToEnvVarName(key);
        return Task.FromResult(!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(envVarName)));
    }

    private static string ConvertToEnvVarName(string key)
    {
        return key
            .Replace("/", "_")
            .Replace("-", "_")
            .ToUpperInvariant();
    }
}
