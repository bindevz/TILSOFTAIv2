namespace TILSOFTAI.Domain.Configuration;

public sealed class ErrorHandlingOptions
{
    public bool ExposeErrorDetail { get; set; } = false;
    public string[] ExposeErrorDetailRoles { get; set; } = new[] { "ai_admin" };
    public bool ExposeErrorDetailInDevelopment { get; set; } = true;
    public int MaxDetailLength { get; set; } = 1024;
}
