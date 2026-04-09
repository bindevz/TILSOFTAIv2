namespace TILSOFTAI.Tools.Abstractions;

public sealed class ToolAdapterRegistry : IToolAdapterRegistry
{
    private readonly Dictionary<string, IToolAdapter> _adapters;

    public ToolAdapterRegistry(IEnumerable<IToolAdapter> adapters)
    {
        _adapters = (adapters ?? throw new ArgumentNullException(nameof(adapters)))
            .ToDictionary(adapter => adapter.AdapterType, StringComparer.OrdinalIgnoreCase);
    }

    public IToolAdapter Resolve(string adapterType)
    {
        if (string.IsNullOrWhiteSpace(adapterType))
        {
            throw new ArgumentException("Adapter type is required.", nameof(adapterType));
        }

        if (_adapters.TryGetValue(adapterType, out var adapter))
        {
            return adapter;
        }

        throw new KeyNotFoundException($"Tool adapter '{adapterType}' is not registered.");
    }

    public IToolAdapter ResolveForCapability(string capabilityKey, string systemId)
    {
        if (!string.IsNullOrWhiteSpace(systemId) && _adapters.TryGetValue(systemId, out var systemAdapter))
        {
            return systemAdapter;
        }

        if (!string.IsNullOrWhiteSpace(capabilityKey))
        {
            var prefix = capabilityKey.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(prefix) && _adapters.TryGetValue(prefix, out var capabilityAdapter))
            {
                return capabilityAdapter;
            }
        }

        if (_adapters.TryGetValue("sql", out var sqlAdapter))
        {
            return sqlAdapter;
        }

        throw new KeyNotFoundException(
            $"No tool adapter is registered for capability '{capabilityKey}' and system '{systemId}'.");
    }
}
