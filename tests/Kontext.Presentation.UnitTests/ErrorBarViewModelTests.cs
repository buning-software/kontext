using Kontext.Testing;

namespace Kontext.Presentation.UnitTests;

[TestFixture]
public class ErrorBarViewModelTests
{
    private ErrorBarViewModel _bar = null!;

    [SetUp]
    public void CreateBar() => _bar = new ErrorBarViewModel();

    [Test]
    public void IsClosedUntilSomethingIsReported()
    {
        Assert.That(_bar.IsOpen, Is.False);
        Assert.That(_bar.Message, Is.Null);
    }

    [Test]
    public void ShowsTheFriendlyMessageAndKeepsTheRawOutputAsDetails()
    {
        _bar.ReportAction(new DetailedKontextException("Authentication failed.", "remote: Invalid username or token"));

        Assert.That(_bar.IsOpen, Is.True);
        Assert.That(_bar.Message, Is.EqualTo("Authentication failed."));
        Assert.That(_bar.Details, Is.EqualTo("remote: Invalid username or token"));
        Assert.That(_bar.HasDetails, Is.True);
        Assert.That(_bar.IsDetailsOpen, Is.False, "details stay behind the toggle");
    }

    [Test]
    public void AnExceptionWithoutDetailsHasNoDetails()
    {
        _bar.ReportAction(new InvalidOperationException("boom"));

        Assert.That(_bar.Message, Is.EqualTo("boom"));
        Assert.That(_bar.HasDetails, Is.False);
    }

    [Test]
    public void TheNewestErrorReplacesTheCurrentOne()
    {
        _bar.ReportPoll("git", new InvalidOperationException("poll failed"));
        _bar.ReportAction(new DetailedKontextException("push failed", "raw"));
        _bar.ReportPoll("sidebar", new InvalidOperationException("sidebar failed"));

        Assert.That(_bar.Message, Is.EqualTo("sidebar failed"));
        Assert.That(_bar.Details, Is.Null, "details of the replaced error do not linger");
    }

    [Test]
    public void ANewErrorCollapsesTheDetails()
    {
        _bar.ReportAction(new DetailedKontextException("one", "raw one"));
        _bar.IsDetailsOpen = true;

        _bar.ReportAction(new DetailedKontextException("two", "raw two"));

        Assert.That(_bar.IsDetailsOpen, Is.False);
    }

    [Test]
    public void DismissingHidesTheError()
    {
        _bar.ReportAction(new InvalidOperationException("push failed"));

        _bar.DismissCommand.Execute(null);

        Assert.That(_bar.IsOpen, Is.False);
        Assert.That(_bar.Message, Is.Null);
        Assert.That(_bar.Details, Is.Null);
    }

    [Test]
    public void ClosingTheBarThroughItsOpenPropertyDismisses()
    {
        _bar.ReportAction(new InvalidOperationException("push failed"));

        _bar.IsOpen = false;

        Assert.That(_bar.Message, Is.Null);
    }

    [Test]
    public void ADismissedActionErrorComesBackWithTheNextFailingAction()
    {
        _bar.ReportAction(new InvalidOperationException("push failed"));
        _bar.Dismiss();

        _bar.ReportAction(new InvalidOperationException("push failed"));

        Assert.That(_bar.IsOpen, Is.True);
    }

    [Test]
    public void ADismissedPollErrorStaysHiddenWhileTheSameMessageRepeats()
    {
        _bar.ReportPoll("git", new InvalidOperationException("not a repository"));
        _bar.Dismiss();

        _bar.ReportPoll("git", new InvalidOperationException("not a repository"));
        _bar.ReportPoll("git", new InvalidOperationException("not a repository"));

        Assert.That(_bar.IsOpen, Is.False);
    }

    [Test]
    public void ADismissedPollErrorReappearsWhenTheMessageChanges()
    {
        _bar.ReportPoll("git", new InvalidOperationException("not a repository"));
        _bar.Dismiss();

        _bar.ReportPoll("git", new InvalidOperationException("git was not found"));

        Assert.That(_bar.Message, Is.EqualTo("git was not found"));
    }

    [Test]
    public void ADismissedPollErrorReappearsAfterASuccessfulPoll()
    {
        _bar.ReportPoll("git", new InvalidOperationException("not a repository"));
        _bar.Dismiss();
        _bar.ClearPoll("git");

        _bar.ReportPoll("git", new InvalidOperationException("not a repository"));

        Assert.That(_bar.IsOpen, Is.True);
    }

    [Test]
    public void ASuccessfulPollClearsItsOwnError()
    {
        _bar.ReportPoll("git", new InvalidOperationException("not a repository"));

        _bar.ClearPoll("git");

        Assert.That(_bar.IsOpen, Is.False);
    }

    [Test]
    public void ASuccessfulPollDoesNotClearAnotherSourcesError()
    {
        _bar.ReportPoll("sidebar", new InvalidOperationException("GitHub is down"));

        _bar.ClearPoll("git");

        Assert.That(_bar.Message, Is.EqualTo("GitHub is down"));
    }

    [Test]
    public void ASuccessfulPollDoesNotClearAnActionError()
    {
        _bar.ReportAction(new InvalidOperationException("push failed"));

        _bar.ClearPoll("git");

        Assert.That(_bar.Message, Is.EqualTo("push failed"));
    }

    [Test]
    public void StartingAnActionClearsTheLastActionErrorButNotAPollError()
    {
        _bar.ReportAction(new InvalidOperationException("push failed"));
        _bar.ClearAction();
        Assert.That(_bar.IsOpen, Is.False);

        _bar.ReportPoll("git", new InvalidOperationException("not a repository"));
        _bar.ClearAction();
        Assert.That(_bar.Message, Is.EqualTo("not a repository"));
    }
}
