using Kontext.Domain;

namespace Kontext.Presentation.UnitTests;

[TestFixture]
public class SplitDiffRowsTests
{
    private static DiffLine Context(int oldNumber, int newNumber, string text) => new(DiffLineKind.Context, oldNumber, newNumber, text);

    private static DiffLine Removed(int oldNumber, string text) => new(DiffLineKind.Removed, oldNumber, null, text);

    private static DiffLine Added(int newNumber, string text) => new(DiffLineKind.Added, null, newNumber, text);

    private static DiffLine Hunk(string text) => new(DiffLineKind.HunkHeader, null, null, text);

    [Test]
    public void NoLinesGiveNoRows() => Assert.That(SplitDiffRows.Build([]), Is.Empty);

    [Test]
    public void AContextLineShowsOnBothSidesWithItsOwnNumbers()
    {
        var rows = SplitDiffRows.Build([Context(4, 6, "same")]);

        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0].Left, Is.EqualTo(new SplitCell(DiffLineKind.Context, 4, "same")));
        Assert.That(rows[0].Right, Is.EqualTo(new SplitCell(DiffLineKind.Context, 6, "same")));
        Assert.That(rows[0].Spanning, Is.Null);
    }

    [Test]
    public void ARemovedLineFollowedByAnAddedLineSitsOnTheSameRow()
    {
        var rows = SplitDiffRows.Build([Removed(3, "old"), Added(3, "new")]);

        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0].Left, Is.EqualTo(new SplitCell(DiffLineKind.Removed, 3, "old")));
        Assert.That(rows[0].Right, Is.EqualTo(new SplitCell(DiffLineKind.Added, 3, "new")));
    }

    [Test]
    public void ARunOfRemovedLinesIsPairedWithTheFollowingAddedLinesInOrder()
    {
        var rows = SplitDiffRows.Build([Removed(1, "a"), Removed(2, "b"), Added(1, "x"), Added(2, "y")]);

        Assert.That(rows.Select(row => (row.Left?.Text, row.Right?.Text)), Is.EqualTo(new[] { ("a", "x"), ("b", "y") }));
    }

    [Test]
    public void ExtraRemovedLinesHaveNothingOnTheRight()
    {
        var rows = SplitDiffRows.Build([Removed(1, "a"), Removed(2, "b"), Added(1, "x")]);

        Assert.That(rows.Select(row => (row.Left?.Text, row.Right?.Text)), Is.EqualTo(new (string?, string?)[] { ("a", "x"), ("b", null) }));
    }

    [Test]
    public void ExtraAddedLinesHaveNothingOnTheLeft()
    {
        var rows = SplitDiffRows.Build([Removed(1, "a"), Added(1, "x"), Added(2, "y")]);

        Assert.That(rows.Select(row => (row.Left?.Text, row.Right?.Text)), Is.EqualTo(new (string?, string?)[] { ("a", "x"), (null, "y") }));
    }

    [Test]
    public void AddedLinesWithoutRemovedLinesStayOnTheRight()
    {
        var rows = SplitDiffRows.Build([Added(1, "x")]);

        Assert.That(rows[0].Left, Is.Null);
        Assert.That(rows[0].Right, Is.EqualTo(new SplitCell(DiffLineKind.Added, 1, "x")));
    }

    [Test]
    public void RemovedLinesWithoutAddedLinesStayOnTheLeft()
    {
        var rows = SplitDiffRows.Build([Removed(1, "a")]);

        Assert.That(rows[0].Left, Is.EqualTo(new SplitCell(DiffLineKind.Removed, 1, "a")));
        Assert.That(rows[0].Right, Is.Null);
    }

    [Test]
    public void ChangesOnEitherSideOfAContextLineAreNotPairedAcrossIt()
    {
        var rows = SplitDiffRows.Build([Removed(1, "a"), Context(2, 1, "mid"), Added(2, "z")]);

        Assert.That(rows.Select(row => (row.Left?.Text, row.Right?.Text)), Is.EqualTo(new (string?, string?)[] { ("a", null), ("mid", "mid"), (null, "z") }));
    }

    [Test]
    public void AHunkHeaderSpansBothSidesAndEndsAPairingRun()
    {
        var header = Hunk("@@ -1 +1 @@");
        var rows = SplitDiffRows.Build([Removed(1, "a"), header, Added(1, "x")]);

        Assert.That(rows, Has.Count.EqualTo(3));
        Assert.That(rows[1].Spanning, Is.EqualTo(header));
        Assert.That(rows[1].Left, Is.Null);
        Assert.That(rows[1].Right, Is.Null);
        Assert.That(rows[2].Left, Is.Null, "the added line after the header has nothing to pair with");
    }

    [Test]
    public void ANoteSpansBothSides()
    {
        var note = new DiffLine(DiffLineKind.Note, null, null, @"\ No newline at end of file");

        var rows = SplitDiffRows.Build([Added(1, "x"), note]);

        Assert.That(rows[1].Spanning, Is.EqualTo(note));
    }
}
