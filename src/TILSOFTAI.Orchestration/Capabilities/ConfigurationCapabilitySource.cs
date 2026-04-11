using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TILSOFTAI.Orchestration.Capabilities;

/// <summary>
/// Sprint 5: Loads capability definitions from IConfiguration (appsettings.json).
/// Reads from the "Capabilities" section and maps each entry to a CapabilityDescriptor.
///
/// Expected configuration format:
/// {
///   "Capabilities": [
///     {
///       "CapabilityKey": "warehouse.inventory.summary",
///       "Domain": "warehouse",
///       "AdapterType": "sql",
///       "Operation": "execute_query",
///       "TargetSystemId": "sql",
///       "ExecutionMode": "readonly",
///       "IntegrationBinding": {
///         "storedProcedure": "dbo.ai_warehouse_inventory_summary"
///       }
///     }
///   ]
/// }
/// </summary>
public sealed class ConfigurationCapabilitySource : ICapabilitySource
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConfigurationCapabilitySource> _logger;

    public ConfigurationCapabilitySource(
        IConfiguration configuration,
        ILogger<ConfigurationCapabilitySource> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string SourceName => "configuration";

    public IReadOnlyList<CapabilityDescriptor> Load()
    {
        var section = _configuration.GetSection("Capabilities");
        if (!section.Exists())
        {
            _logger.LogInformation("ConfigurationCapabilitySource | No 'Capabilities' section found in configuration");
            return Array.Empty<CapabilityDescriptor>();
        }

        var capabilities = new List<CapabilityDescriptor>();
        var children = section.GetChildren().ToList();

        foreach (var child in children)
        {
            var key = child["CapabilityKey"];
            if (string.IsNullOrWhiteSpace(key))
            {
                _logger.LogWarning("ConfigurationCapabilitySource | Skipping entry without CapabilityKey");
                continue;
            }

            var binding = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var bindingSection = child.GetSection("IntegrationBinding");
            if (bindingSection.Exists())
            {
                foreach (var bindingChild in bindingSection.GetChildren())
                {
                    if (!string.IsNullOrWhiteSpace(bindingChild.Value))
                    {
                        binding[bindingChild.Key] = bindingChild.Value;
                    }
                }
            }

            capabilities.Add(new CapabilityDescriptor
            {
                CapabilityKey = key,
                Domain = child["Domain"] ?? string.Empty,
                AdapterType = child["AdapterType"] ?? "sql",
                Operation = child["Operation"] ?? "execute_query",
                TargetSystemId = child["TargetSystemId"] ?? "sql",
                ExecutionMode = child["ExecutionMode"] ?? "readonly",
                IntegrationBinding = binding
            });
        }

        _logger.LogInformation(
            "ConfigurationCapabilitySource | Loaded {Count} capabilities from configuration | Domains: [{Domains}]",
            capabilities.Count,
            string.Join(", ", capabilities.Select(c => c.Domain).Distinct()));

        return capabilities;
    }
}
