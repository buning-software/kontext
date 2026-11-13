using Kontext.Domain;

namespace Kontext.GitAutomation.IntegrationTests;

[TestFixture]
public class WorkspaceCommitterIntegrationTests
{
    private static readonly string[] CommittedFiles = ["README.md", "new.txt"];

    private readonly GitProcess _git = new(new NoGitAuthentication());
    private TempDirectory _temp = null!;
    private WorkspaceChanges _changes = null!;
    private WorkspaceCommitter _committer = null!;
    private string _repo = null!;
    private string _origin = null!;

    [SetUp]
    public async Task CreateRepositories()
    {
        _temp = new TempDirectory();
        _changes = new WorkspaceChanges(_git);
        _committer = new WorkspaceCommitter(_git);

        _origin = _temp.Combine("origin.git");
        Directory.CreateDirectory(_origin);
        await _git.RunCheckedAsync(_origin, ["init", "--bare", "--initial-branch=main"]);

        _repo = _temp.Combine("repo");
        Directory.CreateDirectory(_repo);
        await Git("init", "--initial-branch=main");
        await Git("config", "user.email", "test@example.com");
        await Git("config", "user.name", "Test");
        await Git("config", "core.autocrlf", "false");
        await Git("remote", "add", "origin", _origin);
        Write("README.md", "one\n");
        Write("keep.txt", "keep\n");
        await Git("add", "--", ".");
        await Git("commit", "-m", "initial");
        await Git("push", "--set-upstream", "origin", "main");
        await Git("checkout", "-b", "feature");
    }

    [TearDown]
    public void DeleteRepositories() => _temp.Dispose();

    private async Task Git(params string[] arguments) => await _git.RunCheckedAsync(_repo, arguments);

    private async Task<string> Output(params string[] arguments) => (await _git.RunCheckedAsync(_repo, arguments)).Output;

    private async Task<string> OriginHead(string branch) => (await _git.RunCheckedAsync(_origin, ["rev-parse", branch])).Output;

    private void Write(string name, string content) => File.WriteAllText(System.IO.Path.Combine(_repo, name), content);

    private async Task<FileChange> ChangeOf(string path)
    {
        var changes = await _changes.GetChangesAsync(_repo);
        return changes.Staged.Concat(changes.Unstaged).First(change => change.Path == path);
    }

    private static string[] Lines(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    [Test]
    public async Task CommitsOnlyTheSelectedFiles()
    {
        Write("README.md", "one\ntwo\n");
        Write("other.txt", "other\n");
        Write("new.txt", "new\n");

        await _committer.CommitAsync(_repo, [await ChangeOf("README.md"), await ChangeOf("new.txt")], "Add things");

        Assert.That(await Output("log", "-1", "--format=%s"), Is.EqualTo("Add things"));
        Assert.That(Lines(await Output("show", "--name-only", "--format=", "HEAD")), Is.EquivalentTo(CommittedFiles));
        var remaining = await _changes.GetChangesAsync(_repo);
        Assert.That(remaining.Unstaged.Select(change => change.Path).Single(), Is.EqualTo("other.txt"));
    }

    [Test]
    public async Task LeavesOtherStagedFilesStagedAndOutOfTheCommit()
    {
        Write("README.md", "one\ntwo\n");
        Write("keep.txt", "keep\nmore\n");
        await Git("add", "--", "keep.txt");

        await _committer.CommitAsync(_repo, [await ChangeOf("README.md")], "Only readme");

        Assert.That(await Output("show", "--name-only", "--format=", "HEAD"), Is.EqualTo("README.md"));
        Assert.That(await Output("diff", "--cached", "--name-only"), Is.EqualTo("keep.txt"));
    }

    [Test]
    public async Task CommitsADeletedFile()
    {
        File.Delete(System.IO.Path.Combine(_repo, "keep.txt"));

        await _committer.CommitAsync(_repo, [await ChangeOf("keep.txt")], "Remove keep");

        Assert.That(await Output("show", "--name-status", "--format=", "HEAD"), Is.EqualTo("D\tkeep.txt"));
    }

    [Test]
    public async Task CommitsAFileWhoseDeletionIsAlreadyStaged()
    {
        await Git("rm", "--", "keep.txt");

        await _committer.CommitAsync(_repo, [await ChangeOf("keep.txt")], "Remove keep");

        Assert.That(await Output("show", "--name-status", "--format=", "HEAD"), Is.EqualTo("D	keep.txt"));
    }

    [Test]
    public async Task CommitsARenameWithBothPaths()
    {
        await Git("mv", "keep.txt", "moved.txt");

        await _committer.CommitAsync(_repo, [await ChangeOf("moved.txt")], "Move keep");

        Assert.That(await Output("show", "--name-status", "--format=", "HEAD"), Does.StartWith("R100\tkeep.txt\tmoved.txt"));
        Assert.That(await _changes.GetChangesAsync(_repo), Is.EqualTo(WorkingTreeChanges.Empty));
    }

    [Test]
    public void RejectsNoSelectionAndAnEmptyMessage()
    {
        var change = new FileChange("README.md", null, FileChangeStatus.Modified, IsStaged: false, 1, 0, IsBinary: false);

        Assert.That(() => _committer.CommitAsync(_repo, [], "msg"), Throws.TypeOf<GitAutomationException>());
        Assert.That(() => _committer.CommitAsync(_repo, [change], " "), Throws.InstanceOf<ArgumentException>());
    }

    [Test]
    public async Task ReportsNothingToCommitWhenTheSelectionIsUnchanged()
    {
        var unchanged = new FileChange("README.md", null, FileChangeStatus.Modified, IsStaged: false, 1, 0, IsBinary: false);

        Assert.That(() => _committer.CommitAsync(_repo, [unchanged], "Nothing"), Throws.TypeOf<GitAutomationException>().With.Message.StartsWith("Nothing to commit"));
        Assert.That(await Output("rev-list", "--count", "HEAD"), Is.EqualTo("1"));
    }

    [Test]
    public async Task PushesTheBranchAndSetsTheUpstream()
    {
        Write("README.md", "one\ntwo\n");
        await _committer.CommitAsync(_repo, [await ChangeOf("README.md")], "Change readme");
        Assert.That(await _committer.CountUnpushedAsync(_repo), Is.EqualTo(1));

        await _committer.PushAsync(_repo);

        Assert.That(await OriginHead("feature"), Is.EqualTo(await Output("rev-parse", "feature")));
        Assert.That(await Output("rev-parse", "--abbrev-ref", "feature@{upstream}"), Is.EqualTo("origin/feature"));
        Assert.That(await _committer.CountUnpushedAsync(_repo), Is.Zero);
    }

    [Test]
    public async Task CountsUnpushedCommitsOfABranchWithoutAnUpstream()
    {
        Assert.That(await _committer.CountUnpushedAsync(_repo), Is.Zero);

        Write("README.md", "one\ntwo\n");
        await _committer.CommitAsync(_repo, [await ChangeOf("README.md")], "One");
        Write("README.md", "one\ntwo\nthree\n");
        await _committer.CommitAsync(_repo, [await ChangeOf("README.md")], "Two");

        Assert.That(await _committer.CountUnpushedAsync(_repo), Is.EqualTo(2));
    }

    [Test]
    public async Task AFailedPushIsReportedAndLeavesTheCommitInPlace()
    {
        Write("README.md", "one\ntwo\n");
        await _committer.CommitAsync(_repo, [await ChangeOf("README.md")], "Change readme");
        await Git("remote", "set-url", "origin", _temp.Combine("missing.git"));

        Assert.That(() => _committer.PushAsync(_repo), Throws.TypeOf<GitAutomationException>().With.Message.EqualTo("No remote or upstream is configured for this branch."));
        Assert.That(await _committer.CountUnpushedAsync(_repo), Is.EqualTo(1));
    }

    [Test]
    public async Task ARejectedPushIsExplained()
    {
        var other = _temp.Combine("other");
        await _git.RunCheckedAsync(_temp.Path, ["clone", _origin, "other"]);
        await _git.RunCheckedAsync(other, ["config", "user.email", "o@example.com"]);
        await _git.RunCheckedAsync(other, ["config", "user.name", "Other"]);
        await _git.RunCheckedAsync(other, ["checkout", "-b", "feature"]);
        File.WriteAllText(System.IO.Path.Combine(other, "x.txt"), "x\n");
        await _git.RunCheckedAsync(other, ["add", "--", "x.txt"]);
        await _git.RunCheckedAsync(other, ["commit", "-m", "remote work"]);
        await _git.RunCheckedAsync(other, ["push", "origin", "feature"]);

        Write("README.md", "one\ntwo\n");
        await _committer.CommitAsync(_repo, [await ChangeOf("README.md")], "Local work");

        Assert.That(() => _committer.PushAsync(_repo), Throws.TypeOf<GitAutomationException>().With.Message.StartsWith("Push rejected"));
    }
}
