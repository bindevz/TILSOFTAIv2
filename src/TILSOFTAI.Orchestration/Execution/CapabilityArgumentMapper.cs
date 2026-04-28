using System.Text.RegularExpressions;
using TILSOFTAI.Orchestration.AiRouting.Tools;
using TILSOFTAI.Orchestration.Capabilities;
using TILSOFTAI.Orchestration.Semantic;

namespace TILSOFTAI.Orchestration.Execution;

public sealed class CapabilityArgumentMapper
{
    private static readonly Regex NonAlphaNumeric = new("[^A-Za-z0-9]", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public IReadOnlyDictionary<string, object?> MapModelArgsToProcArgs(
        CapabilityDescriptor capability,
        IReadOnlyDictionary<string, object?> arguments,
        CapabilitySemanticMetadata? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentNullException.ThrowIfNull(arguments);

        var map = BuildMap(capability, metadata);
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, value) in arguments)
        {
            if (string.IsNullOrWhiteSpace(name) || name.StartsWith("__", StringComparison.Ordinal))
            {
                continue;
            }

            var normalized = Normalize(name);
            if (map.TryGetValue(normalized, out var procName))
            {
                result[procName] = value;
                continue;
            }

            result[name.StartsWith('@') ? name : name] = value;
        }

        return result;
    }

    private static Dictionary<string, string> BuildMap(
        CapabilityDescriptor capability,
        CapabilitySemanticMetadata? metadata)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (metadata is not null)
        {
            foreach (var argument in metadata.Arguments)
            {
                AddMapping(map, argument.ArgumentName, argument.ProcParameterName);
                AddMapping(map, CapabilityFunctionNameMapper.MapParameter(argument.ArgumentName), argument.ProcParameterName);
                AddMapping(map, argument.ProcParameterName, argument.ProcParameterName);
            }
        }

        if (capability.ArgumentContract is not null)
        {
            foreach (var allowed in capability.ArgumentContract.AllowedArguments.Concat(capability.ArgumentContract.RequiredArguments))
            {
                AddMapping(map, allowed, allowed);
                AddMapping(map, CapabilityFunctionNameMapper.MapParameter(allowed), allowed);

                var trimmed = allowed.TrimStart('@');
                AddMapping(map, trimmed, allowed);
                AddMapping(map, ToSnakeCase(trimmed), allowed);
            }
        }

        return map;
    }

    private static void AddMapping(Dictionary<string, string> map, string? source, string? target)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        map[Normalize(source)] = target;
    }

    private static string Normalize(string name) =>
        NonAlphaNumeric.Replace(name.Trim().TrimStart('@'), string.Empty).ToLowerInvariant();

    private static string ToSnakeCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var chars = new List<char>(value.Length + 4);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsUpper(c) && i > 0 && (char.IsLower(value[i - 1]) || char.IsDigit(value[i - 1])))
            {
                chars.Add('_');
            }

            chars.Add(char.ToLowerInvariant(c));
        }

        return new string(chars.ToArray());
    }
}
