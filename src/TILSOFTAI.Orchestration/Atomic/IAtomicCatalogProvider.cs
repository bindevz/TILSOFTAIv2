namespace TILSOFTAI.Orchestration.Atomic;

public interface IAtomicCatalogProvider
{
    Task<IReadOnlyList<DatasetCatalogEntry>> GetDatasetsAsync(string tenantId, CancellationToken cancellationToken);
    Task<IReadOnlyList<FieldCatalogEntry>> GetFieldsAsync(string tenantId, string datasetKey, CancellationToken cancellationToken);
    Task<IReadOnlyList<EntityGraphCatalogEntry>> GetEntityGraphsAsync(string tenantId, CancellationToken cancellationToken);
}
