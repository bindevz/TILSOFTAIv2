namespace TILSOFTAI.Domain.Secrets;

/// <summary>
/// Standardized secret key names.
/// </summary>
public static class SecretKeys
{
    public const string SqlConnectionString = "tilsoft/sql/connection-string";
    public const string LlmApiKey = "tilsoft/llm/api-key";
    public const string RedisConnectionString = "tilsoft/redis/connection-string";
    public const string AuthJwksUrl = "tilsoft/auth/jwks-url";
    
    /// <summary>
    /// Maps configuration paths to secret keys.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> ConfigToSecretMap = 
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Sql:ConnectionString"] = SqlConnectionString,
            ["Llm:ApiKey"] = LlmApiKey,
            ["Redis:ConnectionString"] = RedisConnectionString,
            ["Auth:JwksUrl"] = AuthJwksUrl
        };
}
