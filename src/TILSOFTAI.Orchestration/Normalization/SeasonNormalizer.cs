using System.Text.RegularExpressions;

namespace TILSOFTAI.Orchestration.Normalization;

public sealed class SeasonNormalizer
{
    public const string Marker = "__SEASON_2DIGIT__";
    private const int CenturyPivot = 80;

    private static readonly Regex MarkerRegex = new(
        $"{Regex.Escape(Marker)}(?<first>\\d{{2}})\\s*/\\s*(?<second>\\d{{2}}){Regex.Escape(Marker)}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// PATCH 33 FIX: Direct season expansion without markers.
    /// Matches patterns like "24/25", "25/26" and expands to "2024/2025", "2025/2026".
    /// Only expands when both sides are exactly 2 digits and second = first+1 (mod 100).
    /// </summary>
    private static readonly Regex DirectSeasonRegex = new(
        @"(?<!\d)(?<first>\d{2})\s*/\s*(?<second>\d{2})(?!\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Expands 2-digit season pairs directly in raw text (no markers needed).
    /// Safe to apply to promptInput — only adds information, never strips content.
    /// </summary>
    public string ExpandSeasonsDirect(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        return DirectSeasonRegex.Replace(input, match =>
        {
            var firstText = match.Groups["first"].Value;
            var secondText = match.Groups["second"].Value;

            if (!int.TryParse(firstText, out var firstYear) || !int.TryParse(secondText, out var secondYear))
            {
                return match.Value;
            }

            // Only expand if second = first + 1 (valid consecutive season pair)
            var expectedNext = (firstYear + 1) % 100;
            if (secondYear != expectedNext)
            {
                return match.Value;
            }

            var start = ExpandTwoDigitYear(firstYear);
            var end = ExpandTwoDigitYear(secondYear);
            if (end < start)
            {
                end += 100;
            }

            return $"{start}/{end}";
        });
    }

    public string ExpandMarkedSeasons(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        return MarkerRegex.Replace(input, match =>
        {
            var firstText = match.Groups["first"].Value;
            var secondText = match.Groups["second"].Value;

            if (!int.TryParse(firstText, out var firstYear) || !int.TryParse(secondText, out var secondYear))
            {
                return match.Value;
            }

            var start = ExpandTwoDigitYear(firstYear);
            var end = ExpandTwoDigitYear(secondYear);
            if (end < start)
            {
                end += 100;
            }

            return $"{start}/{end}";
        });
    }

    public static string CreateMarker(string firstTwoDigit, string secondTwoDigit)
    {
        return $"{Marker}{firstTwoDigit}/{secondTwoDigit}{Marker}";
    }

    private static int ExpandTwoDigitYear(int year)
    {
        if (year < 0 || year > 99)
        {
            return year;
        }

        return year < CenturyPivot ? 2000 + year : 1900 + year;
    }
}
