namespace TILSOFTAI.Infrastructure.Catalog;

public sealed record CatalogArchiveStorageWriteResult
{
    public string BackendName { get; init; } = string.Empty;
    public string ArchivePath { get; init; } = string.Empty;
    public string StorageUri { get; init; } = string.Empty;
}

public sealed record CatalogArchiveStorageReadResult
{
    public bool Found { get; init; }
    public string BackendName { get; init; } = string.Empty;
    public string ArchivePath { get; init; } = string.Empty;
    public string StorageUri { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
}

public interface IPlatformCatalogArchiveStorage
{
    string BackendName { get; }

    Task<CatalogArchiveStorageWriteResult> WriteAsync(
        string manifestId,
        string content,
        CancellationToken ct);

    Task<CatalogArchiveStorageReadResult> ReadAsync(
        string manifestId,
        CancellationToken ct);
}
