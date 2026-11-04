using System.Globalization;
using System.Text.RegularExpressions;
using Kontext.Domain;

namespace Kontext.GitAutomation;

internal static partial class UnifiedDiffParser
{
    public const int MaxLines = 5000;

    public const string BinaryNotice = "Binary file changed.";

    public const string NoChangesNotice = "No textual changes.";

    /// <summary>Turns the output of <c>git diff</c> for one file into numbered lines, capped at <paramref name="maxLines"/> (hunk headers count).</summary>
    public static FileDiff Parse(string path, string output, int maxLines = MaxLines)
    {
        ArgumentNullException.ThrowIfNull(output);

        var lines = new List<DiffLine>();
        var inHunk = false;
        var truncated = false;
        int oldNumber = 0, newNumber = 0;

        foreach (var raw in output.Split('\n'))
        {
            var line = raw.EndsWith('\r') ? raw[..^1] : raw;

            if (!inHunk)
            {
                if (IsBinaryMarker(line))
                {
                    return new FileDiff(path, [], BinaryNotice);
                }

                if (!line.StartsWith("@@", StringComparison.Ordinal))
                {
                    continue;
                }

                inHunk = true;
            }

            if (line.Length == 0)
            {
                continue;
            }

            if (lines.Count >= maxLines)
            {
                truncated = true;
                break;
            }

            if (line.StartsWith("@@", StringComparison.Ordinal))
            {
                var header = HunkHeader().Match(line);
                if (header.Success)
                {
                    oldNumber = int.Parse(header.Groups["old"].Value, CultureInfo.InvariantCulture);
                    newNumber = int.Parse(header.Groups["new"].Value, CultureInfo.InvariantCulture);
                }

                lines.Add(new DiffLine(DiffLineKind.HunkHeader, null, null, line));
                continue;
            }

            switch (line[0])
            {
                case ' ':
                    lines.Add(new DiffLine(DiffLineKind.Context, oldNumber++, newNumber++, line[1..]));
                    break;
                case '-':
                    lines.Add(new DiffLine(DiffLineKind.Removed, oldNumber++, null, line[1..]));
                    break;
                case '+':
                    lines.Add(new DiffLine(DiffLineKind.Added, null, newNumber++, line[1..]));
                    break;
                case '\\':
                    lines.Add(new DiffLine(DiffLineKind.Note, null, null, line));
                    break;
            }
        }

        if (truncated)
        {
            return new FileDiff(path, lines, string.Create(CultureInfo.InvariantCulture, $"Showing the first {maxLines:N0} lines."));
        }

        return new FileDiff(path, lines, lines.Count == 0 ? NoChangesNotice : null);
    }

    private static bool IsBinaryMarker(string line)
        => line == "GIT binary patch"
            || (line.StartsWith("Binary files ", StringComparison.Ordinal) && line.EndsWith(" differ", StringComparison.Ordinal));

    [GeneratedRegex(@"^@@ -(?<old>\d+)(?:,\d+)? \+(?<new>\d+)(?:,\d+)? @@")]
    private static partial Regex HunkHeader();
}
