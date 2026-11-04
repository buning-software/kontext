using Kontext.Domain;

namespace Kontext.GitAutomation.UnitTests;

[TestFixture]
public class UnifiedDiffParserTests
{
    private const string Header =
        "diff --git a/src/Foo.cs b/src/Foo.cs\n" +
        "index 1111111..2222222 100644\n" +
        "--- a/src/Foo.cs\n" +
        "+++ b/src/Foo.cs\n";

    [Test]
    public void IgnoresTheHeaderBeforeTheFirstHunk()
    {
        var diff = UnifiedDiffParser.Parse("src/Foo.cs", Header + "@@ -1 +1 @@\n-a\n+b\n");

        Assert.That(diff.Lines.Select(line => line.Kind), Is.EqualTo(Expect.Seq(DiffLineKind.HunkHeader, DiffLineKind.Removed, DiffLineKind.Added)));
        Assert.That(diff.Path, Is.EqualTo("src/Foo.cs"));
        Assert.That(diff.Notice, Is.Null);
    }

    [Test]
    public void NumbersLinesFromTheHunkHeader()
    {
        var diff = UnifiedDiffParser.Parse("src/Foo.cs", Header + "@@ -10,3 +10,4 @@ class A\n a\n-b\n+b2\n+c\n d\n");

        Assert.That(diff.Lines, Is.EqualTo(Expect.Seq(
            new DiffLine(DiffLineKind.HunkHeader, null, null, "@@ -10,3 +10,4 @@ class A"),
            new DiffLine(DiffLineKind.Context, 10, 10, "a"),
            new DiffLine(DiffLineKind.Removed, 11, null, "b"),
            new DiffLine(DiffLineKind.Added, null, 11, "b2"),
            new DiffLine(DiffLineKind.Added, null, 12, "c"),
            new DiffLine(DiffLineKind.Context, 12, 13, "d"))));
    }

    [Test]
    public void RestartsTheNumbersAtEachHunk()
    {
        var diff = UnifiedDiffParser.Parse("f", Header + "@@ -1 +1 @@\n-a\n+b\n@@ -50,2 +60,2 @@\n x\n-y\n+z\n");

        var second = diff.Lines.Skip(3).ToList();
        Assert.That(second[0].Kind, Is.EqualTo(DiffLineKind.HunkHeader));
        Assert.That(second[1], Is.EqualTo(new DiffLine(DiffLineKind.Context, 50, 60, "x")));
        Assert.That(second[2], Is.EqualTo(new DiffLine(DiffLineKind.Removed, 51, null, "y")));
        Assert.That(second[3], Is.EqualTo(new DiffLine(DiffLineKind.Added, null, 61, "z")));
    }

    [TestCase("Binary files a/x.png and b/x.png differ\n")]
    [TestCase("index 1..2 100644\nGIT binary patch\nliteral 3\nHcmZ\n")]
    public void ReportsABinaryFileWithANoticeAndNoLines(string body)
    {
        var diff = UnifiedDiffParser.Parse("x.png", "diff --git a/x.png b/x.png\n" + body);

        Assert.That(diff.Lines, Is.Empty);
        Assert.That(diff.Notice, Is.EqualTo("Binary file changed."));
    }

    [Test]
    public void KeepsTheNoNewlineMarkerAsANote()
    {
        var diff = UnifiedDiffParser.Parse("f", Header + "@@ -1 +1 @@\n-a\n\\ No newline at end of file\n+b\n");

        Assert.That(diff.Lines[2], Is.EqualTo(new DiffLine(DiffLineKind.Note, null, null, "\\ No newline at end of file")));
        Assert.That(diff.Lines[3], Is.EqualTo(new DiffLine(DiffLineKind.Added, null, 1, "b")));
    }

    [Test]
    public void StripsTrailingCarriageReturns()
    {
        var diff = UnifiedDiffParser.Parse("f", Header + "@@ -1 +1 @@\r\n-a\r\n+b\r\n");

        Assert.That(diff.Lines.Select(line => line.Text), Is.EqualTo(Expect.Seq("@@ -1 +1 @@", "a", "b")));
    }

    [Test]
    public void CapsTheNumberOfLinesAndSaysSo()
    {
        var body = string.Concat(Enumerable.Range(1, 10).Select(i => $"+line {i}\n"));

        var diff = UnifiedDiffParser.Parse("f", Header + "@@ -0,0 +1,10 @@\n" + body, maxLines: 3);

        Assert.That(diff.Lines, Has.Count.EqualTo(3));
        Assert.That(diff.Notice, Is.EqualTo("Showing the first 3 lines."));
    }

    [Test]
    public void DoesNotMentionACapWhenEverythingFits()
    {
        var diff = UnifiedDiffParser.Parse("f", Header + "@@ -0,0 +1,3 @@\n+a\n+b\n+c\n", maxLines: 4);

        Assert.That(diff.Lines, Has.Count.EqualTo(4));
        Assert.That(diff.Notice, Is.Null);
    }

    [Test]
    public void ReportsNoTextualChangesForEmptyOutput()
    {
        var diff = UnifiedDiffParser.Parse("f", string.Empty);

        Assert.That(diff.Lines, Is.Empty);
        Assert.That(diff.Notice, Is.EqualTo("No textual changes."));
    }

    [Test]
    public void ReportsNoTextualChangesForAHeaderOnlyDiffSuchAsAModeChange()
    {
        var diff = UnifiedDiffParser.Parse("f", "diff --git a/f b/f\nold mode 100644\nnew mode 100755\n");

        Assert.That(diff.Lines, Is.Empty);
        Assert.That(diff.Notice, Is.EqualTo("No textual changes."));
    }
}
