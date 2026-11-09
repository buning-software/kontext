namespace Kontext.Domain;

public sealed record DiffLine(DiffLineKind Kind, int? OldNumber, int? NewNumber, string Text);
