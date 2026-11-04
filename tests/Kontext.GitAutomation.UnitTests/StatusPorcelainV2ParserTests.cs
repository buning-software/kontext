namespace Kontext.GitAutomation.UnitTests;

[TestFixture]
public class StatusPorcelainV2ParserTests
{
    [Test]
    public void ParsesAnUnstagedModification()
    {
        var entries = StatusPorcelainV2Parser.Parse("1 .M N... 100644 100644 100644 aaa bbb src/Foo.cs\0");

        var entry = entries.Single();
        Assert.That(entry.Path, Is.EqualTo("src/Foo.cs"));
        Assert.That(entry.Index, Is.EqualTo('.'));
        Assert.That(entry.Worktree, Is.EqualTo('M'));
        Assert.That(entry.OriginalPath, Is.Null);
        Assert.That(entry.IsUntracked, Is.False);
        Assert.That(entry.IsConflicted, Is.False);
    }

    [Test]
    public void ParsesAStagedAddition()
    {
        var entry = StatusPorcelainV2Parser.Parse("1 A. N... 000000 100644 100644 000 bbb new.cs\0").Single();

        Assert.That(entry.Index, Is.EqualTo('A'));
        Assert.That(entry.Worktree, Is.EqualTo('.'));
    }

    [TestCase("a b.txt")]
    [TestCase("héllo wörld.txt")]
    [TestCase("-leading dash.txt")]
    public void KeepsSpacesAndUnusualCharactersInThePath(string path)
    {
        var entry = StatusPorcelainV2Parser.Parse($"1 M. N... 100644 100644 100644 aaa bbb {path}\0").Single();

        Assert.That(entry.Path, Is.EqualTo(path));
    }

    [Test]
    public void ParsesARenameWithItsOriginalPath()
    {
        var entry = StatusPorcelainV2Parser.Parse("2 R. N... 100644 100644 100644 aaa bbb R100 new.txt\0old.txt\0").Single();

        Assert.That(entry.Path, Is.EqualTo("new.txt"));
        Assert.That(entry.OriginalPath, Is.EqualTo("old.txt"));
        Assert.That(entry.Index, Is.EqualTo('R'));
    }

    [Test]
    public void ParsesAnUntrackedFile()
    {
        var entry = StatusPorcelainV2Parser.Parse("? new.txt\0").Single();

        Assert.That(entry.Path, Is.EqualTo("new.txt"));
        Assert.That(entry.IsUntracked, Is.True);
        Assert.That(entry.Index, Is.EqualTo('.'));
        Assert.That(entry.Worktree, Is.EqualTo('?'));
    }

    [Test]
    public void ParsesAConflict()
    {
        var entry = StatusPorcelainV2Parser.Parse("u UU N... 100644 100644 100644 100644 aaa bbb ccc c.txt\0").Single();

        Assert.That(entry.Path, Is.EqualTo("c.txt"));
        Assert.That(entry.IsConflicted, Is.True);
    }

    [Test]
    public void SkipsIgnoredAndHeaderRecords()
    {
        var entries = StatusPorcelainV2Parser.Parse("# branch.oid abc\0! ignored.log\0? kept.txt\0");

        Assert.That(entries.Select(entry => entry.Path), Is.EqualTo(Expect.Seq("kept.txt")));
    }

    [Test]
    public void ReturnsNothingForEmptyOutput()
    {
        Assert.That(StatusPorcelainV2Parser.Parse(string.Empty), Is.Empty);
    }

    [Test]
    public void ParsesSeveralRecordsInOrder()
    {
        const string output =
            "1 .M N... 100644 100644 100644 aaa bbb a.cs\0" +
            "2 R. N... 100644 100644 100644 aaa bbb R100 c.cs\0b.cs\0" +
            "? d.cs\0";

        var entries = StatusPorcelainV2Parser.Parse(output);

        Assert.That(entries.Select(entry => entry.Path), Is.EqualTo(Expect.Seq("a.cs", "c.cs", "d.cs")));
    }
}
