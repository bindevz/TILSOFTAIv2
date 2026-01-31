namespace TILSOFTAI.Domain.Configuration;

public sealed class CorsOptions
{
    public bool Enabled { get; set; } = false;
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
    public bool AllowCredentials { get; set; } = true;
    public string[] AllowedMethods { get; set; } = new[] { "GET", "POST", "PUT", "DELETE", "OPTIONS" };
    public string[] AllowedHeaders { get; set; } = new[] { "Content-Type", "Authorization" };
}
