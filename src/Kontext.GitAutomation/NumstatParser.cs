using System.Globalization;

namespace Kontext.GitAutomation;

/// <summary>Line counts of one file; both null when git reports the file as binary.</summary>
internal readonly record struct NumstatEntry(int? Additions, int? Deletions);

internal static class NumstatParser
{
    /// <summary>Parses <c>git diff --numstat -z</c>. The result is keyed by the (new) path.</summary>
    public static IReadOnlyDictionary<string, NumstatEntry> Parse(string output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var counts = new Dictionary<string, NumstatEntry>(StringComparer.Ordinal);
        var records = output.Split('\0');

        for (var i = 0; i < records.Length; i++)
        {
            var parts = records[i].Split('\t', 3);
            if (parts.Length < 3)
            {
                continue;
            }

            var path = parts[2];
            if (path.Length == 0)
            {
                // A rename: the old and new paths follow as two separate records.
                if (i + 2 >= records.Length)
                {
                    break;
                }

                path = records[i + 2];
                i += 2;
            }

            counts[path] = new NumstatEntry(Count(parts[0]), Count(parts[1]));
        }

        return counts;
    }

    private static int? Count(string text)
        => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : null;
}
