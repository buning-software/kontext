using CommunityToolkit.Mvvm.ComponentModel;

namespace Kontext.Presentation;

/// <summary>
/// One account card in the repository filter: the repositories on one GitHub server, under a checkbox that shows or
/// hides all of them at once.
/// </summary>
public sealed class RepositoryAccountFilterViewModel(
    string host,
    string? login,
    IReadOnlyList<RepositoryFilterItemViewModel> repositories,
    Action<RepositoryAccountFilterViewModel, bool> setAllVisible) : ObservableObject
{
    private string? _login = login;

    public string Host { get; } = host;

    public IReadOnlyList<RepositoryFilterItemViewModel> Repositories { get; } = repositories;

    /// <summary>The signed-in username and its server (<c>jelle - github.com</c>), or just the server when the username is not known.</summary>
    public string Title => _login is null ? Host : $"{_login} - {Host}";

    /// <summary>
    /// True when every repository is shown, false when none is, null (partly checked) otherwise. Checking shows them all,
    /// unchecking hides them all.
    /// </summary>
    public bool? AreAllVisible
    {
        get => Repositories.All(repository => repository.IsVisible) ? true
            : Repositories.Any(repository => repository.IsVisible) ? null
            : false;
        set
        {
            if (value is { } visible && value != AreAllVisible)
            {
                setAllVisible(this, visible);
            }
        }
    }

    internal void SetLogin(string? login)
    {
        if (_login == login)
        {
            return;
        }

        _login = login;
        OnPropertyChanged(nameof(Title));
    }

    internal void RepositoriesChanged() => OnPropertyChanged(nameof(AreAllVisible));
}
