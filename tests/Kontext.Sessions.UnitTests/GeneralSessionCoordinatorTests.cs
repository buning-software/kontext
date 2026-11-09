using Kontext.Domain;
using Kontext.Testing;

namespace Kontext.Sessions.UnitTests;

[TestFixture]
public class GeneralSessionCoordinatorTests
{
    private FakeTerminalService _terminals = null!;
    private FakeClaudeLauncher _claude = null!;
    private GeneralSessionCoordinator _coordinator = null!;
    private int _changes;

    [SetUp]
    public void CreateFakes()
    {
        _terminals = new FakeTerminalService();
        _claude = new FakeClaudeLauncher();
        _coordinator = new GeneralSessionCoordinator(_terminals, _claude, new ImmediateDispatcher());
        _changes = 0;
        _coordinator.Changed += (_, _) => _changes++;
    }

    [TearDown]
    public void DisposeFakes() => _terminals.Dispose();

    [Test]
    public async Task StartsClaudeInTheUserProfileFolder()
    {
        await _coordinator.StartAsync();

        Assert.That(_claude.GeneralCalls, Is.EqualTo(Expect.Seq(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))));
        Assert.That(_terminals.Starts, Has.Count.EqualTo(1));
        Assert.That(_coordinator.Current, Is.SameAs(_terminals.LastSession));
        Assert.That(_changes, Is.EqualTo(1));
    }

    [Test]
    public async Task KeepsTheRunningSessionInsteadOfStartingASecond()
    {
        await _coordinator.StartAsync();
        var first = _coordinator.Current;

        await _coordinator.StartAsync();

        Assert.That(_terminals.Starts, Has.Count.EqualTo(1));
        Assert.That(_coordinator.Current, Is.SameAs(first));
    }

    [Test]
    public async Task SaysSoWhenTheSessionEndsAndStartsAFreshOneOnRequest()
    {
        await _coordinator.StartAsync();
        var first = _terminals.LastSession!;
        var before = _changes;

        first.End(0);
        await AsyncAssert.EventuallyAsync(() => _changes > before, "the change");

        Assert.That(_coordinator.Current!.HasExited, Is.True);

        await _coordinator.StartAsync();

        Assert.That(_terminals.Starts, Has.Count.EqualTo(2));
        Assert.That(_coordinator.Current, Is.Not.SameAs(first));
        Assert.That(_coordinator.Current!.HasExited, Is.False);
    }

    [Test]
    public async Task ReportsTheErrorWhenTheTerminalCannotStart()
    {
        _terminals.FailWith = new InvalidOperationException("no terminal");

        await _coordinator.StartAsync();

        Assert.That(_coordinator.Current, Is.Null);
        Assert.That(_coordinator.StartError, Is.EqualTo("no terminal"));
        Assert.That(_changes, Is.EqualTo(1));
    }

    [Test]
    public async Task ClearsTheErrorOnceASessionStarts()
    {
        _terminals.FailWith = new InvalidOperationException("no terminal");
        await _coordinator.StartAsync();
        _terminals.FailWith = null;

        await _coordinator.StartAsync();

        Assert.That(_coordinator.StartError, Is.Null);
        Assert.That(_coordinator.Current, Is.Not.Null);
    }

    [Test]
    public async Task ClosesTheEndedSessionWhenStartingANewOne()
    {
        await _coordinator.StartAsync();
        var first = _terminals.LastSession!;
        first.End(0);

        await _coordinator.StartAsync();

        Assert.That(_terminals.Closed, Does.Contain(first.Id));
    }

    [Test]
    public async Task DropsTheEndedSessionWhenARestartFailsSoTheErrorIsShown()
    {
        await _coordinator.StartAsync();
        _terminals.LastSession!.End(0);
        _terminals.FailWith = new InvalidOperationException("no terminal");

        await _coordinator.StartAsync();

        Assert.That(_coordinator.Current, Is.Null);
        Assert.That(_coordinator.StartError, Is.EqualTo("no terminal"));
    }

    [Test]
    public async Task StartsOnlyOneSessionWhileTheFirstIsStillStarting()
    {
        using var deferred = new DeferredTerminalService();
        var coordinator = new GeneralSessionCoordinator(deferred, _claude, new ImmediateDispatcher());

        var first = coordinator.StartAsync();
        await coordinator.StartAsync();
        deferred.Complete(TerminalSessionId.New());
        await first;

        Assert.That(deferred.Starts, Is.EqualTo(1));
        Assert.That(coordinator.Current, Is.Not.Null);
    }

    [Test]
    public async Task RethrowsCancellationAndCanStartAgainAfterwards()
    {
        _terminals.FailWith = new OperationCanceledException();

        Assert.That(async () => await _coordinator.StartAsync(), Throws.InstanceOf<OperationCanceledException>());
        Assert.That(_coordinator.Current, Is.Null);

        _terminals.FailWith = null;
        await _coordinator.StartAsync();

        Assert.That(_coordinator.Current, Is.Not.Null);
    }
}
