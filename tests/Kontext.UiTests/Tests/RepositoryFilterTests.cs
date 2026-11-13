using WinUia.Patterns;
using static Kontext.UiTests.Application.FakeGitHub;

namespace Kontext.UiTests.Tests;

public sealed class RepositoryFilterTests : KontextFixture
{
    [Test]
    public void HidingARepositoryRemovesItsGroupUntilTheFiltersAreCleared()
    {
        Assert.Fail("Temporary failure to validate the recording artifact (issue #32). Remove before merging.");
        var filter = App.Sidebar.OpenFilter();

        filter.Hide(Memento);

        Eventually(() => !App.Sidebar.HasGroup(Memento));
        Assert.That(App.Sidebar.HasGroup(Gallery), Is.True);

        filter.ClearFilters();
        filter.Close();

        Eventually(() => App.Sidebar.HasGroup(Memento));
    }

    [Test]
    public void UncheckingAnAccountHidesAllItsRepositoriesAndCheckingItShowsThemAgain()
    {
        var filter = App.Sidebar.OpenFilter();

        filter.HideAccount(Account);

        Eventually(() => Repositories.All(repository => !App.Sidebar.HasGroup(repository)));
        Assert.That(filter.Repository(Memento).TogglePattern.State, Is.EqualTo(ToggleState.Off));

        filter.ShowAccount(Account);

        Eventually(() => Repositories.All(App.Sidebar.HasGroup));
    }
}
