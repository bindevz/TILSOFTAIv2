using System.Text.RegularExpressions;

namespace TILSOFTAI.Orchestration.Normalization;

public sealed class SeasonNormalizer
{
    public const string Marker = "__SEASON_2DIGIT__";
    private const int CenturyPivot = 80;

    private static readonly Regex MarkerRegex = new(
        $"{Regex.Escape(Marker)}(?<first>\\d{{2}})\\s*/\\s*(?<second>\\d{{2}}){Regex.Escape(Marker)}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

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
