using Kontext.Domain;

namespace Kontext.Presentation.UnitTests;

[TestFixture]
public class WorkingTreeChangesTests
{
    private static FileChange Change(string path = "src/Foo.cs", int additions = 3, bool staged = false)
        => new(path, null, FileChangeStatus.Modified, staged, additions, 1, IsBinary: false);

    private static WorkingTreeChanges Tree(params FileChange[] unstaged)
        => new([Change("README.md", staged: true)], unstaged);

    [Test]
    public void TwoResultsWithTheSameFilesAreEqual()
    {
        var first = Tree(Change("a.cs"), Change("b.cs"));
        var second = Tree(Change("a.cs"), Change("b.cs"));

        Assert.That(first, Is.EqualTo(second));
        Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
    }

    [Test]
    public void AResultWithADifferentCountIsNotEqual()
    {
        var first = Tree(Change("a.cs", additions: 3));
        var second = Tree(Change("a.cs", additions: 4));

        Assert.That(first, Is.Not.EqualTo(second));
    }

    [Test]
    public void EmptyHasNoFiles()
    {
        Assert.That(WorkingTreeChanges.Empty.Staged, Is.Empty);
        Assert.That(WorkingTreeChanges.Empty.Unstaged, Is.Empty);
    }

    [Test]
    public void TwoDiffsWithTheSameLinesAreEqual()
    {
        var first = new FileDiff("a.cs", [new DiffLine(DiffLineKind.Added, null, 1, "x")]);
        var second = new FileDiff("a.cs", [new DiffLine(DiffLineKind.Added, null, 1, "x")]);
        var other = new FileDiff("a.cs", [new DiffLine(DiffLineKind.Added, null, 1, "y")]);

        Assert.That(first, Is.EqualTo(second));
        Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        Assert.That(first, Is.Not.EqualTo(other));
    }
}
