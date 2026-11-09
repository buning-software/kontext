using Kontext.Domain;

namespace Kontext.Presentation;

/// <summary>One side of a split diff row. <paramref name="Kind"/> is Context, Removed (left) or Added (right).</summary>
public sealed record SplitCell(DiffLineKind Kind, int? Number, string Text);
