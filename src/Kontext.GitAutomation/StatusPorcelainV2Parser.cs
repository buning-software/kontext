namespace Kontext.GitAutomation;

internal static class StatusPorcelainV2Parser
{
    // Number of space-separated fields before the path (the path itself may contain spaces).
    private const int OrdinaryFields = 9;
    private const int RenamedFields = 10;
    private const int UnmergedFields = 11;

    public static IReadOnlyList<StatusEntry> Parse(string output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var records = output.Split('\0');
        var entries = new List<StatusEntry>();

        for (var i = 0; i < records.Length; i++)
        {
            var record = records[i];
            if (record.Length < 2)
            {
                continue;
            }

            switch (record[0])
            {
                case '1':
                    AddWithPath(entries, record, OrdinaryFields, originalPath: null, conflicted: false);
                    break;
                case '2':
                    // A rename or copy is followed by its original path as a separate NUL-terminated record.
                    var original = i + 1 < records.Length ? records[++i] : null;
                    AddWithPath(entries, record, RenamedFields, original, conflicted: false);
                    break;
                case 'u':
                    AddWithPath(entries, record, UnmergedFields, originalPath: null, conflicted: true);
                    break;
                case '?':
                    entries.Add(new StatusEntry(record[2..], null, '.', '?', IsUntracked: true, IsConflicted: false));
                    break;
            }
        }

        return entries;
    }

    private static void AddWithPath(List<StatusEntry> entries, string record, int fieldCount, string? originalPath, bool conflicted)
    {
        var fields = record.Split(' ', fieldCount);
        if (fields.Length < fieldCount || fields[1].Length != 2)
        {
            return;
        }

        entries.Add(new StatusEntry(fields[^1], originalPath, fields[1][0], fields[1][1], IsUntracked: false, conflicted));
    }
}
