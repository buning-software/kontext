using Kontext.Domain;

namespace Kontext.Presentation;

public static class SplitDiffRows
{
    /// <summary>Lays a unified diff out side by side. A run of removed lines is paired, in order, with the added lines that follow it.</summary>
    public static IReadOnlyList<SplitDiffRow> Build(IReadOnlyList<DiffLine> lines)
    {
        var rows = new List<SplitDiffRow>(lines.Count);
        var removed = new List<DiffLine>();
        var added = new List<DiffLine>();

        foreach (var line in lines)
        {
            if (line.Kind == DiffLineKind.Added)
            {
                added.Add(line);
                continue;
            }

            // Anything else ends the pending run; a removed line then starts the next one.
            if (line.Kind != DiffLineKind.Removed || added.Count > 0)
            {
                Flush(rows, removed, added);
            }

            if (line.Kind == DiffLineKind.Removed)
            {
                removed.Add(line);
            }
            else if (line.Kind == DiffLineKind.Context)
            {
                rows.Add(new SplitDiffRow(new SplitCell(DiffLineKind.Context, line.OldNumber, line.Text), new SplitCell(DiffLineKind.Context, line.NewNumber, line.Text)));
            }
            else
            {
                rows.Add(new SplitDiffRow(null, null, line));
            }
        }

        Flush(rows, removed, added);
        return rows;
    }

    private static void Flush(List<SplitDiffRow> rows, List<DiffLine> removed, List<DiffLine> added)
    {
        var count = Math.Max(removed.Count, added.Count);
        for (var index = 0; index < count; index++)
        {
            var left = index < removed.Count ? new SplitCell(DiffLineKind.Removed, removed[index].OldNumber, removed[index].Text) : null;
            var right = index < added.Count ? new SplitCell(DiffLineKind.Added, added[index].NewNumber, added[index].Text) : null;
            rows.Add(new SplitDiffRow(left, right));
        }

        removed.Clear();
        added.Clear();
    }
}
