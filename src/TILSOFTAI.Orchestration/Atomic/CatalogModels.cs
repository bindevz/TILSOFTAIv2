namespace TILSOFTAI.Orchestration.Atomic;

public sealed class DatasetCatalogEntry
{
    public string DatasetKey { get; set; } = string.Empty;
    public string? TenantId { get; set; }
    public string? BaseObject { get; set; }
    public string? TimeColumn { get; set; }
    public bool IsEnabled { get; set; }
}

public sealed class FieldCatalogEntry
{
    public string DatasetKey { get; set; } = string.Empty;
    public string FieldKey { get; set; } = string.Empty;
    public string PhysicalColumn { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsMetric { get; set; }
    public bool IsDimension { get; set; }
    public string? AllowedAggregations { get; set; }
    public bool IsFilterable { get; set; }
    public bool IsGroupable { get; set; }
    public bool IsSortable { get; set; }
    public bool IsEnabled { get; set; }
}

public sealed class EntityGraphCatalogEntry
{
    public string GraphKey { get; set; } = string.Empty;
    public string FromDatasetKey { get; set; } = string.Empty;
    public string ToDatasetKey { get; set; } = string.Empty;
    public string JoinType { get; set; } = string.Empty;
    public string JoinConditionTemplate { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
}
