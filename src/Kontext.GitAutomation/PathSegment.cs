namespace Kontext.GitAutomation;

internal static class PathSegment
{
    public static string Safe(string segment)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(segment.Select(character => invalid.Contains(character) ? '_' : character));
    }
}
