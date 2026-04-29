using System.Text;

namespace TILSOFTAI.Orchestration.AiRouting.Tools;

public static class CapabilityFunctionNameMapper
{
    private static readonly HashSet<string> BlockedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "run_proc",
        "query_data",
        "get_data",
        "execute_tool",
        "execute_sql",
        "execute_capability",
        "model_query"
    };

    public static string Map(string functionName, string capabilityKey)
    {
        var source = string.IsNullOrWhiteSpace(functionName) ? capabilityKey : functionName;
        var mapped = ToSnakeCase(source);

        if (BlockedNames.Contains(mapped))
        {
            throw new InvalidOperationException($"Generic model-facing function name is not allowed: {mapped}");
        }

        return mapped;
    }

    public static string MapParameter(string argumentName)
    {
        if (argumentName.Equals("item", StringComparison.OrdinalIgnoreCase)) return "item_no";
        if (argumentName.Equals("customer", StringComparison.OrdinalIgnoreCase)) return "customer_code";
        if (argumentName.Equals("supplier", StringComparison.OrdinalIgnoreCase)) return "supplier_code";
        if (argumentName.Equals("warehouse", StringComparison.OrdinalIgnoreCase)) return "warehouse_code";
        if (argumentName.Equals("modelCode", StringComparison.OrdinalIgnoreCase)) return "modelCode";
        if (argumentName.Equals("modelCodes", StringComparison.OrdinalIgnoreCase)) return "modelCodes";
        return ToSnakeCase(argumentName);
    }

    private static string ToSnakeCase(string value)
    {
        var builder = new StringBuilder(value.Length + 8);
        var previousWasSeparator = true;

        foreach (var ch in value.Trim())
        {
            if (char.IsLetterOrDigit(ch))
            {
                if (char.IsUpper(ch) && builder.Length > 0 && !previousWasSeparator)
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(ch));
                previousWasSeparator = false;
            }
            else if (!previousWasSeparator)
            {
                builder.Append('_');
                previousWasSeparator = true;
            }
        }

        return builder.ToString().Trim('_');
    }
}
