using System.Text.RegularExpressions;

namespace ArtifactBrowser.Client.Utilities;

/// <summary>Compares strings the way humans expect numbers to sort ("file2" before "file10").</summary>
public sealed class NaturalSortComparer : IComparer<string>
{
    public static readonly NaturalSortComparer Instance = new();

    private static readonly Regex TokenPattern = new(@"\d+|\D+", RegexOptions.Compiled);

    public int Compare(string? x, string? y)
    {
        if (x == y)
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        var xTokens = TokenPattern.Matches(x);
        var yTokens = TokenPattern.Matches(y);
        var count = Math.Min(xTokens.Count, yTokens.Count);

        for (var i = 0; i < count; i++)
        {
            var xToken = xTokens[i].Value;
            var yToken = yTokens[i].Value;

            var bothNumeric = char.IsDigit(xToken[0]) && char.IsDigit(yToken[0]);
            int comparison;

            if (bothNumeric)
            {
                var xNum = xToken.TrimStart('0');
                var yNum = yToken.TrimStart('0');
                comparison = xNum.Length != yNum.Length
                    ? xNum.Length.CompareTo(yNum.Length)
                    : string.CompareOrdinal(xNum, yNum);
            }
            else
            {
                comparison = string.Compare(xToken, yToken, StringComparison.OrdinalIgnoreCase);
            }

            if (comparison != 0)
            {
                return comparison;
            }
        }

        return xTokens.Count.CompareTo(yTokens.Count);
    }
}
