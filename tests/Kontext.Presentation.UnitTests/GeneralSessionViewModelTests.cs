using Kontext.Domain;
using Kontext.Testing;

namespace Kontext.Presentation.UnitTests;

[TestFixture]
public class GeneralSessionViewModelTests
{
    private FakeGeneralSessionCoordinator _coordinator = null!;
    private GeneralSessionViewModel _general = null!;

    [SetUp]
    public void Create()
    {
        _coordinator = new FakeGeneralSessionCoordinator();
        _general = new GeneralSessionViewModel(_coordinator);
    }

    [TearDown]
    public void Dispose() => _general.Dispose();

    [Test]
    public void StartsCoveredWithoutASession()
    {
        Assert.That(_general.IsCovered, Is.True);
        Assert.That(_general.HasSession, Is.False);
        Assert.That(_general.VisibleSession, Is.Null);
        Assert.That(_coordinator.Starts, Is.Zero);
    }

    [Test]
    public void OpeningItStartsASessionTheFirstTimeAndShowsIt()
    {
        _general.Open();

        Assert.That(_coordinator.Starts, Is.EqualTo(1));
        Assert.That(_general.IsCovered, Is.False);
        Assert.That(_general.VisibleSession, Is.SameAs(_coordinator.Current));
    }

    [Test]
    public void OpeningItAgainKeepsTheRunningSession()
    {
        _general.Open();
        var session = _general.VisibleSession;
        _general.IsCovered = true;

        _general.Open();

        Assert.That(_coordinator.Starts, Is.EqualTo(1));
        Assert.That(_general.VisibleSession, Is.SameAs(session));
    }

    [Test]
    public void HidesTheSessionWhileCoveredAndSaysSo()
    {
        _general.Open();
        var changed = new List<string?>();
        _general.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _general.IsCovered = true;

        Assert.That(_general.VisibleSession, Is.Null);
        Assert.That(_general.HasSession, Is.True);
        Assert.That(changed, Does.Contain(nameof(GeneralSessionViewModel.VisibleSession)));
    }

    [Test]
    public void ShowsTheEndedStateWhenTheSessionExits()
    {
        _general.Open();
        Assert.That(_general.HasEnded, Is.False);

        ((FakeTerminalSession)_coordinator.Current!).End(0);
        _coordinator.RaiseChanged();

        Assert.That(_general.HasEnded, Is.True);
    }

    [Test]
    public async Task StartNewSessionReplacesTheEndedSession()
    {
        _general.Open();
        var ended = (FakeTerminalSession)_coordinator.Current!;
        ended.End(0);
        _coordinator.RaiseChanged();

        await _general.StartNewSessionCommand.ExecuteAsync(null);

        Assert.That(_coordinator.Starts, Is.EqualTo(2));
        Assert.That(_general.Session, Is.Not.SameAs(ended));
        Assert.That(_general.HasEnded, Is.False);
    }

    [Test]
    public void ShowsTheStartErrorInsteadOfATerminal()
    {
        _coordinator.StartError = "no claude";

        _coordinator.RaiseChanged();

        Assert.That(_general.Placeholder, Is.EqualTo("no claude"));
        Assert.That(_general.HasNoSession, Is.True);
    }

    [Test]
    public void OffersAnotherAttemptWhenTheFirstStartFailed()
    {
        _coordinator.StartError = "no claude";

        _coordinator.RaiseChanged();

        Assert.That(_general.HasEnded, Is.True, "the Start new session button is shown");
        Assert.That(_general.EndedText, Is.EqualTo("Claude could not be started."));
    }

    [Test]
    public void SaysTheSessionEndedWhenItExits()
    {
        _general.Open();

        ((FakeTerminalSession)_coordinator.Current!).End(0);
        _coordinator.RaiseChanged();

        Assert.That(_general.EndedText, Is.EqualTo("Session ended."));
    }
}
