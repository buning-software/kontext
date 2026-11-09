using Kontext.Domain;

namespace Kontext.Presentation;

/// <summary>
/// One row of a split diff: the old text on the left, the new text on the right. A hunk header or note has no
/// sides and instead spans the whole row through <paramref name="Spanning"/>.
/// </summary>
public sealed record SplitDiffRow(SplitCell? Left, SplitCell? Right, DiffLine? Spanning = null);
