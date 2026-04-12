namespace TILSOFTAI.Domain.Configuration;

public sealed class CatalogCertificationOptions
{
    public string EnvironmentName { get; set; } = "development";
    public string[] ProductionLikeEnvironments { get; set; } = { "prod", "production", "staging" };
    public bool RequireCertificationEvidenceForProductionLikePromotion { get; set; } = true;
    public string[] RequiredEvidenceKinds { get; set; } =
    {
        "runbook_execution",
        "preview_failure_drill",
        "version_conflict_drill",
        "duplicate_submit_drill",
        "sql_apply_outage_drill",
        "fallback_risk_drill",
        "operator_signoff"
    };

    public int PreviewSuccessSloPercent { get; set; } = 99;
    public int SubmitSuccessSloPercent { get; set; } = 99;
    public int ApproveSuccessSloPercent { get; set; } = 99;
    public int ApplySuccessSloPercent { get; set; } = 99;
    public int RollbackReadyMinutes { get; set; } = 30;
    public int VersionConflictAlertThresholdPerHour { get; set; } = 3;
    public int DuplicateSubmitAlertThresholdPerHour { get; set; } = 5;
    public int ApplyFailureAlertThresholdPerHour { get; set; } = 1;
    public int RollbackSurgeAlertThresholdPerHour { get; set; } = 2;
}
