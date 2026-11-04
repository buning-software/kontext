using Kontext.Domain;
using Kontext.TestSupport;

namespace Kontext.GitAutomation.IntegrationTests;

[TestFixture]
public class WorkspaceChangesIntegrationTests
{
    private const string Original = "one\ntwo\nthree\n";

    private readonly GitProcess _git = new(new NoGitAuthentication());
    private TempDirectory _temp = null!;
    private WorkspaceChanges _changes = null!;
    private string _repo = null!;

    [SetUp]
    public async Task CreateRepository()
    {
        _temp = new TempDirectory();
        _changes = new WorkspaceChanges(_git);

        _repo = _temp.Combine("repo");
        Directory.CreateDirectory(_repo);
        await Git("init", "--initial-branch=main");
        await Git("config", "user.email", "test@example.com");
        await Git("config", "user.name", "Test");
        await Git("config", "core.autocrlf", "false");
        Write("README.md", Original);
        await Git("add", "--", ".");
        await Git("commit", "-m", "initial");
    }

    [TearDown]
    public void DeleteRepository() => _temp.Dispose();

    private async Task Git(params string[] arguments) => await _git.RunCheckedAsync(_repo, arguments);

    private void Write(string name, string content) => File.WriteAllText(System.IO.Path.Combine(_repo, name), content);

    private async Task CommitAsync(string name, string content)
    {
        Write(name, content);
        await Git("add", "--", name);
        await Git("commit", "-m", $"add {name}");
    }

    private static IEnumerable<string> ChangedLines(FileDiff diff, DiffLineKind kind)
        => diff.Lines.Where(line => line.Kind == kind).Select(line => line.Text);

    [Test]
    public async Task ReportsAModifiedFileAsUnstaged()
    {
        Write("README.md", "one\nTWO\nthree\nfour\n");

        var changes = await _changes.GetChangesAsync(_repo);

        Assert.That(changes.Staged, Is.Empty);
        Assert.That(changes.Unstaged.Single(), Is.EqualTo(new FileChange("README.md", null, FileChangeStatus.Modified, IsStaged: false, 2, 1, IsBinary: false)));
    }

    [Test]
    public async Task ReportsAStagedFileAsStaged()
    {
        Write("README.md", "one\nTWO\nthree\nfour\n");
        await Git("add", "--", "README.md");

        var changes = await _changes.GetChangesAsync(_repo);

        Assert.That(changes.Unstaged, Is.Empty);
        Assert.That(changes.Staged.Single(), Is.EqualTo(new FileChange("README.md", null, FileChangeStatus.Modified, IsStaged: true, 2, 1, IsBinary: false)));
    }

    [Test]
    public async Task ReportsAPartiallyStagedFileInBothGroupsEachWithItsOwnDiff()
    {
        Write("README.md", "one\nTWO\nthree\n");
        await Git("add", "--", "README.md");
        Write("README.md", "one\nTWO\nthree\nfour\n");

        var changes = await _changes.GetChangesAsync(_repo);

        var staged = changes.Staged.Single();
        var unstaged = changes.Unstaged.Single();
        Assert.That((staged.Additions, staged.Deletions), Is.EqualTo((1, 1)));
        Assert.That((unstaged.Additions, unstaged.Deletions), Is.EqualTo((1, 0)));

        var stagedDiff = await _changes.GetDiffAsync(_repo, staged);
        var unstagedDiff = await _changes.GetDiffAsync(_repo, unstaged);
        Assert.That(ChangedLines(stagedDiff, DiffLineKind.Added), Is.EqualTo(Expect.Seq("TWO")));
        Assert.That(ChangedLines(stagedDiff, DiffLineKind.Removed), Is.EqualTo(Expect.Seq("two")));
        Assert.That(ChangedLines(unstagedDiff, DiffLineKind.Added), Is.EqualTo(Expect.Seq("four")));
        Assert.That(ChangedLines(unstagedDiff, DiffLineKind.Removed), Is.Empty);
    }

    [Test]
    public async Task ReportsAnUntrackedFileWithItsLineCount()
    {
        Write("notes.txt", "a\nb\nc\n");

        var changes = await _changes.GetChangesAsync(_repo);

        Assert.That(changes.Staged, Is.Empty);
        Assert.That(changes.Unstaged.Single(), Is.EqualTo(new FileChange("notes.txt", null, FileChangeStatus.Untracked, IsStaged: false, 3, 0, IsBinary: false)));
    }

    [Test]
    public async Task ReportsADeletedFile()
    {
        File.Delete(System.IO.Path.Combine(_repo, "README.md"));

        var changes = await _changes.GetChangesAsync(_repo);

        Assert.That(changes.Unstaged.Single(), Is.EqualTo(new FileChange("README.md", null, FileChangeStatus.Deleted, IsStaged: false, 0, 3, IsBinary: false)));
    }

    [Test]
    public async Task ReportsARenameWithItsOriginalPath()
    {
        await Git("mv", "README.md", "docs.md");

        var changes = await _changes.GetChangesAsync(_repo);

        Assert.That(changes.Unstaged, Is.Empty);
        var renamed = changes.Staged.Single();
        Assert.That(renamed.Status, Is.EqualTo(FileChangeStatus.Renamed));
        Assert.That(renamed.Path, Is.EqualTo("docs.md"));
        Assert.That(renamed.OriginalPath, Is.EqualTo("README.md"));
        Assert.That(renamed.IsStaged, Is.True);

        var diff = await _changes.GetDiffAsync(_repo, renamed);
        Assert.That(diff.Lines.Where(line => line.Kind is DiffLineKind.Added or DiffLineKind.Removed), Is.Empty, "a pure rename has no textual hunk");
    }

    [Test]
    public async Task ReportsABinaryFileWithoutCounts()
    {
        File.WriteAllBytes(System.IO.Path.Combine(_repo, "img.bin"), [1, 0, 2, 0]);
        await Git("add", "--", "img.bin");
        await Git("commit", "-m", "binary");
        File.WriteAllBytes(System.IO.Path.Combine(_repo, "img.bin"), [1, 0, 3, 0]);

        var change = (await _changes.GetChangesAsync(_repo)).Unstaged.Single();

        Assert.That(change.IsBinary, Is.True);
        Assert.That(change.Additions, Is.Null);
        Assert.That(change.Deletions, Is.Null);

        var diff = await _changes.GetDiffAsync(_repo, change);
        Assert.That(diff.Lines, Is.Empty);
        Assert.That(diff.Notice, Is.EqualTo("Binary file changed."));
    }

    [Test]
    public async Task ReportsAnUntrackedBinaryFileWithoutCountsAndWithANotice()
    {
        File.WriteAllBytes(System.IO.Path.Combine(_repo, "new.bin"), [7, 0, 7]);

        var change = (await _changes.GetChangesAsync(_repo)).Unstaged.Single();

        Assert.That((change.IsBinary, change.Additions, change.Deletions), Is.EqualTo((true, (int?)null, (int?)null)));
        Assert.That((await _changes.GetDiffAsync(_repo, change)).Notice, Is.EqualTo("Binary file changed."));
    }

    [Test]
    public async Task HandlesFileNamesWithSpacesDashesAndUnicode()
    {
        const string tracked = "-sp ace ü.txt";
        await CommitAsync(tracked, "x\n");
        Write(tracked, "x\ny\n");
        Write("héllo wörld.txt", "new\n");

        var changes = await _changes.GetChangesAsync(_repo);

        Assert.That(changes.Unstaged.Select(change => change.Path), Is.EqualTo(Expect.Seq("-sp ace ü.txt", "héllo wörld.txt")));
        var diff = await _changes.GetDiffAsync(_repo, changes.Unstaged[0]);
        Assert.That(diff.Lines.Single(line => line.Kind == DiffLineKind.Added).Text, Is.EqualTo("y"));
    }

    [Test]
    public async Task ReturnsNothingForACleanWorktree()
    {
        Assert.That(await _changes.GetChangesAsync(_repo), Is.EqualTo(WorkingTreeChanges.Empty));
    }

    [Test]
    public async Task DoesNotListIgnoredFiles()
    {
        await CommitAsync(".gitignore", "*.log\n");
        Write("debug.log", "noise\n");

        Assert.That(await _changes.GetChangesAsync(_repo), Is.EqualTo(WorkingTreeChanges.Empty));
    }

    [Test]
    public async Task TheDiffOfAModifiedFileHasNumberedLines()
    {
        Write("README.md", "one\nTWO\nthree\nfour\n");
        var change = (await _changes.GetChangesAsync(_repo)).Unstaged.Single();

        var diff = await _changes.GetDiffAsync(_repo, change);

        Assert.That(diff.Notice, Is.Null);
        Assert.That(diff.Lines[0].Kind, Is.EqualTo(DiffLineKind.HunkHeader));
        Assert.That(diff.Lines.Skip(1), Is.EqualTo(Expect.Seq(
            new DiffLine(DiffLineKind.Context, 1, 1, "one"),
            new DiffLine(DiffLineKind.Removed, 2, null, "two"),
            new DiffLine(DiffLineKind.Added, null, 2, "TWO"),
            new DiffLine(DiffLineKind.Context, 3, 3, "three"),
            new DiffLine(DiffLineKind.Added, null, 4, "four"))));
    }

    [Test]
    public async Task TheDiffOfAnUntrackedFileIsAllAddedLines()
    {
        Write("notes.txt", "a\nb\n");
        var change = (await _changes.GetChangesAsync(_repo)).Unstaged.Single();

        var diff = await _changes.GetDiffAsync(_repo, change);

        Assert.That(diff.Notice, Is.Null);
        Assert.That(diff.Lines, Is.EqualTo(Expect.Seq(
            new DiffLine(DiffLineKind.Added, null, 1, "a"),
            new DiffLine(DiffLineKind.Added, null, 2, "b"))));
    }

    [Test]
    public async Task AnUntrackedFileOver256KbHasNoCountsAndAPreviewNotice()
    {
        Write("big.txt", string.Concat(Enumerable.Repeat("xxxxxxxxxxxxxxx\n", 20_000)));

        var change = (await _changes.GetChangesAsync(_repo)).Unstaged.Single();

        Assert.That((change.IsBinary, change.Additions, change.Deletions), Is.EqualTo((false, (int?)null, (int?)null)));
        var diff = await _changes.GetDiffAsync(_repo, change);
        Assert.That(diff.Lines, Is.Empty);
        Assert.That(diff.Notice, Is.EqualTo("File too large to preview."));
    }

    [Test]
    public async Task StillListsEverythingWhenAnUntrackedFileIsOpenForWriting()
    {
        // For example a log file that Claude, a dev server or a test run keeps appending to.
        await using var writer = new FileStream(System.IO.Path.Combine(_repo, "busy.log"), FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        await writer.WriteAsync("line\n"u8.ToArray());
        await writer.FlushAsync();
        Write("other.txt", "x\n");

        var changes = await _changes.GetChangesAsync(_repo);

        Assert.That(changes.Unstaged.Select(change => change.Path), Is.EqualTo(Expect.Seq("busy.log", "other.txt")));
        var busy = changes.Unstaged[0];
        Assert.That(busy.Additions, Is.EqualTo(1));
        Assert.That((await _changes.GetDiffAsync(_repo, busy)).Lines.Single().Text, Is.EqualTo("line"));
    }

    [Test]
    public async Task ListsAnExclusivelyLockedUntrackedFileWithoutCountsInsteadOfFailing()
    {
        await using var locked = new FileStream(System.IO.Path.Combine(_repo, "locked.bin"), FileMode.Create, FileAccess.Write, FileShare.None);
        await locked.WriteAsync("data\n"u8.ToArray());
        await locked.FlushAsync();
        Write("other.txt", "x\n");

        var changes = await _changes.GetChangesAsync(_repo);

        Assert.That(changes.Unstaged.Select(change => change.Path), Is.EqualTo(Expect.Seq("locked.bin", "other.txt")));
        Assert.That((changes.Unstaged[0].Additions, changes.Unstaged[0].Deletions), Is.EqualTo(((int?)null, (int?)null)));
        var diff = await _changes.GetDiffAsync(_repo, changes.Unstaged[0]);
        Assert.That(diff.Lines, Is.Empty);
        Assert.That(diff.Notice, Is.EqualTo("File not available."));
    }

    [Test]
    public async Task CountsTheLinesOfOnlyTheFirstUntrackedFilesWhenThereAreHundreds()
    {
        for (var i = 0; i < 210; i++)
        {
            Write($"file{i:D3}.txt", "a\n");
        }

        var changes = await _changes.GetChangesAsync(_repo);

        Assert.That(changes.Unstaged, Has.Count.EqualTo(210), "every file is still listed");
        Assert.That(changes.Unstaged.Count(change => change.Additions is not null), Is.EqualTo(200));
        Assert.That(changes.Unstaged.Count(change => change.Additions is null), Is.EqualTo(10));
    }

    [Test]
    public async Task ListsAConflictedFileWithoutADiff()
    {
        await Git("checkout", "-b", "side");
        await CommitAsync("README.md", "one\nside\nthree\n");
        await Git("checkout", "main");
        await CommitAsync("README.md", "one\nmain\nthree\n");
        var merge = await _git.RunAsync(_repo, ["merge", "side"]);
        Assert.That(merge.Succeeded, Is.False, "the merge must conflict for this test to mean anything");

        var changes = await _changes.GetChangesAsync(_repo);

        var conflicted = changes.Unstaged.Single();
        Assert.That(conflicted.Status, Is.EqualTo(FileChangeStatus.Conflicted));
        var diff = await _changes.GetDiffAsync(_repo, conflicted);
        Assert.That(diff.Lines, Is.Empty);
        Assert.That(diff.Notice, Is.EqualTo("Merge conflict. Resolve it in the terminal."));
    }

    [Test]
    public async Task ThrowsGitAutomationExceptionWhenTheFolderIsNotARepository()
    {
        var plain = _temp.Combine("plain");
        Directory.CreateDirectory(plain);

        await Assert.ThatAsync(() => _changes.GetChangesAsync(plain), Throws.TypeOf<GitAutomationException>());
    }
}
