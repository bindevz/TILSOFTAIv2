namespace TILSOFTAI.Domain.Configuration;

public sealed class CorsOptions
{
    public bool Enabled { get; set; } = false;
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
    public bool AllowCredentials { get; set; } = true;
    public string[] AllowedMethods { get; set; } = new[] { "GET", "POST", "PUT", "DELETE", "OPTIONS" };
    public string[] AllowedHeaders { get; set; } = new[] { "Content-Type", "Authorization" };

    /// <summary>
    /// Normalizes AllowedOrigins by trimming whitespace and removing trailing slashes.
    /// ASP.NET Core CORS requires origins without trailing slashes for exact matching.
    /// </summary>
    public void Normalize()
    {
        if (AllowedOrigins == null || AllowedOrigins.Length == 0)
            return;

        for (int i = 0; i < AllowedOrigins.Length; i++)
        {
            AllowedOrigins[i] = NormalizeOrigin(AllowedOrigins[i]);
        }
    }

    private static string NormalizeOrigin(string origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
            return origin;

        var trimmed = origin.Trim();

        // Wildcard doesn't need further normalization
        if (trimmed == "*")
            return trimmed;

        // Remove trailing slash (ASP.NET Core CORS requirement)
        return trimmed.TrimEnd('/');
    }
}
