using CommunityToolkit.Mvvm.ComponentModel;

namespace Kontext.Presentation;

/// <summary>One repository checkbox in the filter. <see cref="Name"/> is the <c>RepositoryRef.DisplayName</c> it is remembered by.</summary>
public sealed class RepositoryFilterItemViewModel(string name, string displayName, bool isVisible, Action<RepositoryFilterItemViewModel> changed)
    : ObservableObject
{
    private bool _isVisible = isVisible;

    public string Name { get; } = name;

    /// <summary>The name shown in the filter, without the server: the account card already says which server it is on.</summary>
    public string DisplayName { get; } = displayName;

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (SetProperty(ref _isVisible, value))
            {
                changed(this);
            }
        }
    }

    /// <summary>Changes the checkbox for a whole-account change, which the sidebar applies and saves once.</summary>
    internal void ShowWithoutReporting(bool visible) => SetProperty(ref _isVisible, visible, nameof(IsVisible));
}
