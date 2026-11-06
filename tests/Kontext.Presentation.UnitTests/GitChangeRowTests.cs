using Kontext.Domain;

namespace Kontext.Presentation.UnitTests;

[TestFixture]
public class GitChangeRowTests
{
    private static GitChangeRow File(string path = "src/Views/Foo.cs", FileChangeStatus status = FileChangeStatus.Modified, int? additions = 12, int? deletions = 3)
        => new(path, new FileChange(path, null, status, IsStaged: false, additions, deletions, IsBinary: false));

    [TestCase(FileChangeStatus.Added, "+", true, false, false)]
    [TestCase(FileChangeStatus.Untracked, "+", true, false, false)]
    [TestCase(FileChangeStatus.Modified, "●", false, true, false)]
    [TestCase(FileChangeStatus.Renamed, "●", false, true, false)]
    [TestCase(FileChangeStatus.Deleted, "−", false, false, true)]
    [TestCase(FileChangeStatus.Conflicted, "!", false, false, true)]
    public void ShowsAColouredGlyphForEachStatus(FileChangeStatus status, string glyph, bool added, bool changed, bool removed)
    {
        var row = File(status: status);

        Assert.That(row.StatusGlyph, Is.EqualTo(glyph));
        Assert.That(row.IsAddedGlyph, Is.EqualTo(added));
        Assert.That(row.IsChangedGlyph, Is.EqualTo(changed));
        Assert.That(row.IsRemovedGlyph, Is.EqualTo(removed));
    }

    [Test]
    public void SplitsTheFileNameFromItsFolder()
    {
        var nested = File("src/Views/Foo.cs");
        var root = File("README.md");

        Assert.That(nested.FileName, Is.EqualTo("Foo.cs"));
        Assert.That(nested.Folder, Is.EqualTo("src/Views"));
        Assert.That(root.FileName, Is.EqualTo("README.md"));
        Assert.That(root.Folder, Is.Empty);
    }

    [Test]
    public void HidesCountsWhenTheyAreUnknown()
    {
        Assert.That(File(additions: null, deletions: null).HasCounts, Is.False);
        Assert.That(File(additions: 0, deletions: 0).HasCounts, Is.True);
    }

    [Test]
    public void FormatsTheCounts()
    {
        var row = File(additions: 12, deletions: 3);

        Assert.That(row.AdditionsText, Is.EqualTo("+12"));
        Assert.That(row.DeletionsText, Is.EqualTo("−3"));
    }

    [Test]
    public void AHeaderRowHasNoFile()
    {
        var header = new GitChangeRow("Staged (1)", null);

        Assert.That(header.IsHeader, Is.True);
        Assert.That(header.Text, Is.EqualTo("Staged (1)"));
        Assert.That(header.StatusGlyph, Is.Empty);
        Assert.That(header.IsAddedGlyph || header.IsChangedGlyph || header.IsRemovedGlyph, Is.False);
        Assert.That(header.HasCounts, Is.False);
        Assert.That(File().IsHeader, Is.False);
    }
}
