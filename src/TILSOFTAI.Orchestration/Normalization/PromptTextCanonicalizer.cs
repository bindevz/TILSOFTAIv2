using System.Text;
using System.Text.RegularExpressions;

namespace TILSOFTAI.Orchestration.Normalization;

/// <summary>
/// PATCH 29.05: Canonicalizes whitespace in prompt text for deterministic processing.
/// - Normalizes CRLF/CR to LF
/// - Collapses multiple spaces/tabs/NBSP to single space
/// - Trims each line
/// - Collapses excessive blank lines (max 1)
/// </summary>
public static partial class PromptTextCanonicalizer
{
    private const char Space = ' ';
    private const char Nbsp = '\u00A0';
    private const char Tab = '\t';
    
    // Match multiple whitespace (spaces, tabs, NBSP) in a row
    [GeneratedRegex(@"[ \t\u00A0]+", RegexOptions.Compiled)]
    private static partial Regex MultipleSpacesRegex();
    
    // Match 3+ consecutive newlines (collapse to 2, which means 1 blank line)
    [GeneratedRegex(@"\n{3,}", RegexOptions.Compiled)]
    private static partial Regex ExcessiveNewlinesRegex();

    /// <summary>
    /// Canonicalizes the input text for consistent whitespace handling.
    /// </summary>
    public static string Canonicalize(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        // Step 1: Normalize line endings (CRLF/CR -> LF)
        var normalized = input.Replace("\r\n", "\n").Replace("\r", "\n");
        
        // Step 2: Process each line - trim and collapse internal whitespace
        var lines = normalized.Split('\n');
        var sb = new StringBuilder(input.Length);
        
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            
            // Replace NBSP and tabs with regular spaces
            line = line.Replace(Nbsp, Space).Replace(Tab, Space);
            
            // Collapse multiple spaces to single
            line = MultipleSpacesRegex().Replace(line, " ");
            
            // Trim the line
            line = line.Trim();
            
            sb.Append(line);
            if (i < lines.Length - 1)
            {
                sb.Append('\n');
            }
        }
        
        var result = sb.ToString();
        
        // Step 3: Collapse excessive blank lines (3+ newlines -> 2 newlines)
        result = ExcessiveNewlinesRegex().Replace(result, "\n\n");
        
        // Step 4: Final trim
        return result.Trim();
    }
    
    /// <summary>
    /// Checks if canonicalization would result in empty output (guards against stripping all content).
    /// </summary>
    public static bool WouldBeEmpty(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return true;
        }
        
        // Quick check: if there's any alphanumeric character, it won't be empty
        foreach (var c in input)
        {
            if (char.IsLetterOrDigit(c))
            {
                return false;
            }
        }
        
        // Slow path: actually canonicalize and check
        return string.IsNullOrWhiteSpace(Canonicalize(input));
    }
}
