using Kontext.Domain;
using Kontext.Kernel;
using Kontext.Testing;

namespace Kontext.Claude.UnitTests;

[TestFixture]
public class ClaudeActivityReporterTests
{
    private static readonly IssueKey Key = new("acme/memento", 4);

    private InMemoryIssueWorkTracker _tracker = null!;
    private InMemoryIssueSessionRegistry _registry = null!;
    private FakeTerminalSession _session = null!;
    private FakeSessionStore _store = null!;
    private ClaudeActivityReporter _reporter = null!;

    [SetUp]
    public void CreateReporter()
    {
        _tracker = new InMemoryIssueWorkTracker();
        _registry = new InMemoryIssueSessionRegistry();
        _session = new FakeTerminalSession(TerminalSessionId.New());
        _registry.Register(Key, _session);
        _tracker.Report(Key, new IssueWorkState(IssueWorkStatus.Ready, "kontext/4-x", @"C:\wt\4-x"));
        _store = new FakeSessionStore();
        _reporter = new ClaudeActivityReporter(_registry, _tracker, _store, new ImmediateDispatcher());
    }

    private void Hook(string name, string? notificationType = null) => _reporter.OnHook(new ClaudeHookEvent(_session.Id, name, notificationType));

    [Test]
    public void FollowsAWholeConversationFromThePromptToTheEndOfTheAnswer()
    {
        var seen = _tracker.Record(Key);

        Hook("UserPromptSubmit");
        Hook("PreToolUse");
        Hook("PermissionRequest");
        Hook("PostToolUse");
        Hook("Stop");

        Assert.That(seen, Is.EqualTo(Expect.Seq(IssueWorkStatus.Running, IssueWorkStatus.NeedsInput, IssueWorkStatus.Running, IssueWorkStatus.NeedsInput)));
    }

    [Test]
    public void KeepsTheWorkspaceDetailsWhenTheStatusChanges()
    {
        Hook("UserPromptSubmit");

        var state = _tracker.StateOf(Key);
        Assert.That(state.BranchName, Is.EqualTo("kontext/4-x"));
        Assert.That(state.WorktreePath, Is.EqualTo(@"C:\wt\4-x"));
    }

    [Test]
    public void SaysNothingWhenTheStatusWouldNotChange()
    {
        Hook("UserPromptSubmit");
        var changes = 0;
        _tracker.Changed += (_, _) => changes++;

        Hook("PreToolUse");
        Hook("PostToolUse");

        Assert.That(changes, Is.Zero, "a busy session makes many events but only one change");
    }

    [Test]
    public void IgnoresEventsOfSessionsItDoesNotKnow()
    {
        _reporter.OnHook(new ClaudeHookEvent(TerminalSessionId.New(), "UserPromptSubmit"));

        Assert.That(_tracker.StateOf(Key).Status, Is.EqualTo(IssueWorkStatus.Ready));
    }

    [Test]
    public void IgnoresLateEventsOnceTheSessionHasEnded()
    {
        _tracker.Report(Key, _tracker.StateOf(Key) with { Status = IssueWorkStatus.Done, SessionEnded = true });

        Hook("UserPromptSubmit");

        Assert.That(_tracker.StateOf(Key).Status, Is.EqualTo(IssueWorkStatus.Done));
    }

    [Test]
    public void IgnoresEventsThatChangeNothing()
    {
        Hook("SessionStart");

        Assert.That(_tracker.StateOf(Key).Status, Is.EqualTo(IssueWorkStatus.Ready));
    }

    [Test]
    public void RefusesANullEvent()
    {
        Assert.That(() => _reporter.OnHook(null!), Throws.ArgumentNullException);
    }

    private static StoredSession Stored(TerminalSessionId id) => new(Key, id, @"C:\wt\4-x", "kontext/4-x");

    [Test]
    public void RemembersTheSessionClaudeIsReallyUsingWhenResumingGaveItANewId()
    {
        var earlier = TerminalSessionId.New();
        var now = Guid.NewGuid();
        _store.Save(Stored(earlier));

        _reporter.OnHook(new ClaudeHookEvent(_session.Id, "UserPromptSubmit", ClaudeSession: now));

        var saved = _store.Find(Key)!;
        Assert.That(saved.SessionId, Is.EqualTo(new TerminalSessionId(now)), "the next resume must continue this conversation, not the one it was copied from");
        Assert.That(saved.WorktreePath, Is.EqualTo(@"C:\wt\4-x"));
        Assert.That(saved.BranchName, Is.EqualTo("kontext/4-x"));
    }

    [Test]
    public void LeavesTheStoredSessionAloneWhileClaudeKeepsTheSameId()
    {
        var id = TerminalSessionId.New();
        var stored = Stored(id);
        _store.Save(stored);

        _reporter.OnHook(new ClaudeHookEvent(_session.Id, "UserPromptSubmit", ClaudeSession: id.Value));

        Assert.That(_store.Find(Key), Is.SameAs(stored));
    }

    [Test]
    public void LeavesTheStoredSessionAloneWhenTheEventNamesNoSession()
    {
        var stored = Stored(TerminalSessionId.New());
        _store.Save(stored);

        Hook("UserPromptSubmit");

        Assert.That(_store.Find(Key), Is.SameAs(stored));
    }

    [Test]
    public void StoresNothingForAnIssueThatHasNoSavedSession()
    {
        _reporter.OnHook(new ClaudeHookEvent(_session.Id, "UserPromptSubmit", ClaudeSession: Guid.NewGuid()));

        Assert.That(_store.Find(Key), Is.Null);
    }

    [Test]
    public void StillRemembersTheSessionFromALateEventAfterItEnded()
    {
        _store.Save(Stored(TerminalSessionId.New()));
        _tracker.Report(Key, _tracker.StateOf(Key) with { Status = IssueWorkStatus.Ready, SessionEnded = true });
        var now = Guid.NewGuid();

        _reporter.OnHook(new ClaudeHookEvent(_session.Id, "Stop", ClaudeSession: now));

        Assert.That(_store.Find(Key)!.SessionId, Is.EqualTo(new TerminalSessionId(now)));
    }
}
