namespace Kontext.GitAutomation.UnitTests;

[TestFixture]
public class NumstatParserTests
{
    [Test]
    public void ParsesCounts()
    {
        var counts = NumstatParser.Parse("3\t1\tsrc/Foo.cs\0");

        Assert.That(counts["src/Foo.cs"], Is.EqualTo(new NumstatEntry(3, 1)));
    }

    [Test]
    public void ReportsBinaryFilesWithNullCounts()
    {
        var counts = NumstatParser.Parse("-\t-\timg.png\0");

        Assert.That(counts["img.png"], Is.EqualTo(new NumstatEntry(null, null)));
    }

    [Test]
    public void KeysARenameByItsNewPath()
    {
        var counts = NumstatParser.Parse("0\t0\t\0old.txt\0new.txt\0");

        Assert.That(counts.Keys, Is.EqualTo(Expect.Seq("new.txt")));
        Assert.That(counts["new.txt"], Is.EqualTo(new NumstatEntry(0, 0)));
    }

    [Test]
    public void KeepsSpacesInThePath()
    {
        var counts = NumstatParser.Parse("2\t0\ta b.txt\0");

        Assert.That(counts["a b.txt"], Is.EqualTo(new NumstatEntry(2, 0)));
    }

    [Test]
    public void ParsesSeveralRecordsIncludingARenameInTheMiddle()
    {
        var counts = NumstatParser.Parse("1\t1\ta.cs\0" + "5\t2\t\0old.cs\0new.cs\0" + "7\t0\tz.cs\0");

        Assert.That(counts.Keys, Is.EquivalentTo(Expect.Seq("a.cs", "new.cs", "z.cs")));
        Assert.That(counts["new.cs"], Is.EqualTo(new NumstatEntry(5, 2)));
        Assert.That(counts["z.cs"], Is.EqualTo(new NumstatEntry(7, 0)));
    }

    [Test]
    public void ReturnsNothingForEmptyOutput()
    {
        Assert.That(NumstatParser.Parse(string.Empty), Is.Empty);
    }
}
