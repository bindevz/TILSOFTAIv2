namespace TILSOFTAI.Domain.Configuration;

public sealed class CatalogControlPlaneOptions
{
    public string[] SubmitRoles { get; set; } = { "platform_catalog_admin" };
    public string[] ApproveRoles { get; set; } = { "platform_catalog_approver" };
    public string[] ApplyRoles { get; set; } = { "platform_catalog_approver" };
    public string[] HighRiskApproveRoles { get; set; } = { "platform_catalog_senior_approver" };
    public string[] BreakGlassRoles { get; set; } = Array.Empty<string>();
    public string[] ProductionLikeEnvironments { get; set; } = { "prod", "production", "staging" };
    public string EnvironmentName { get; set; } = "development";
    public bool AllowSelfApproval { get; set; }
    public bool RequireExpectedVersionForExistingRecordsInProductionLike { get; set; } = true;
    public bool RequireIndependentApplyInProductionLike { get; set; } = true;
    public bool AllowBreakGlass { get; set; }
    public int MinBreakGlassJustificationLength { get; set; } = 20;
}
