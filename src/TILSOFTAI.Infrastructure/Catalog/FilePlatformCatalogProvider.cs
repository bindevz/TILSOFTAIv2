using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;
using TILSOFTAI.Orchestration.Capabilities;

namespace TILSOFTAI.Infrastructure.Catalog;

public sealed class FilePlatformCatalogProvider : IPlatformCatalogProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly PlatformCatalogOptions _options;
    private readonly ILogger<FilePlatformCatalogProvider> _logger;
    private readonly object _gate = new();
    private PlatformCatalogSnapshot? _snapshot;

    public FilePlatformCatalogProvider(
        IOptions<PlatformCatalogOptions> options,
        ILogger<FilePlatformCatalogProvider> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public PlatformCatalogSnapshot Load()
    {
        if (!_options.Enabled)
        {
            return new PlatformCatalogSnapshot();
        }

        if (_snapshot is not null)
        {
            return _snapshot;
        }

        lock (_gate)
        {
            if (_snapshot is not null)
            {
                return _snapshot;
            }

            var path = ResolveCatalogPath(_options.CatalogPath);
            if (!File.Exists(path))
            {
                _logger.LogWarning(
                    "PlatformCatalog | Catalog file not found | Path: {CatalogPath}",
                    path);
                _snapshot = new PlatformCatalogSnapshot();
                return _snapshot;
            }

            var json = File.ReadAllText(path);
            var document = JsonSerializer.Deserialize<PlatformCatalogDocument>(json, JsonOptions)
                ?? new PlatformCatalogDocument();

            var connections = new Dictionary<string, ExternalConnectionOptions>(StringComparer.OrdinalIgnoreCase);
            if (document.ExternalConnections?.Connections is not null)
            {
                foreach (var pair in document.ExternalConnections.Connections)
                {
                    connections[pair.Key] = pair.Value;
                }
            }

            _snapshot = new PlatformCatalogSnapshot
            {
                Capabilities = document.Capabilities ?? Array.Empty<CapabilityDescriptor>(),
                ExternalConnections = connections
            };

            _logger.LogInformation(
                "PlatformCatalog | Loaded durable records | Path: {CatalogPath} | Capabilities: {CapabilityCount} | Connections: {ConnectionCount} | Version: {Version}",
                path,
                _snapshot.Capabilities.Count,
                _snapshot.ExternalConnections.Count,
                document.Version ?? "unspecified");

            return _snapshot;
        }
    }

    private static string ResolveCatalogPath(string catalogPath)
    {
        if (Path.IsPathRooted(catalogPath))
        {
            return catalogPath;
        }

        foreach (var root in CandidateRoots())
        {
            var current = root;
            for (var depth = 0; depth < 8 && !string.IsNullOrWhiteSpace(current); depth++)
            {
                var candidate = Path.GetFullPath(Path.Combine(current, catalogPath));
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                current = Directory.GetParent(current)?.FullName;
            }
        }

        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), catalogPath));
    }

    private static IEnumerable<string> CandidateRoots()
    {
        yield return Directory.GetCurrentDirectory();
        yield return AppContext.BaseDirectory;
    }
}

public sealed class PlatformCatalogDocument
{
    public string? Version { get; init; }
    public string? Owner { get; init; }
    public string? ChangeControl { get; init; }
    public IReadOnlyList<CapabilityDescriptor>? Capabilities { get; init; }
    public ExternalConnectionCatalogOptions? ExternalConnections { get; init; }
}
