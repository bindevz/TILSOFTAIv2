namespace TILSOFTAI.Domain.Configuration;

public sealed class CatalogControlPlaneOptions
{
    public string[] SubmitRoles { get; set; } = { "platform_catalog_admin" };
    public string[] ApproveRoles { get; set; } = { "platform_catalog_approver" };
    public bool AllowSelfApproval { get; set; }
}
