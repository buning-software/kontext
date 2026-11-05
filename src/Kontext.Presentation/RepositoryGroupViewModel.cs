using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Kontext.Presentation;

public sealed class RepositoryGroupViewModel(string name, IEnumerable<IssueItemViewModel> items, bool isExpanded = true, Action<RepositoryGroupViewModel>? expandedChanged = null) : ObservableObject
{
    private bool _isExpanded = isExpanded;

    public string Name { get; } = name;

    public ObservableCollection<IssueItemViewModel> Items { get; } = new(items);

    /// <summary>
    /// Set by the menu when the user expands or collapses the group; a change is reported so it can be remembered,
    /// and raised so the menu shows the remembered value again if it ever differs.
    /// </summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (!SetProperty(ref _isExpanded, value))
            {
                return;
            }

            expandedChanged?.Invoke(this);
        }
    }

    /// <summary>False while the group is filtered out or has no search match; the menu keeps its row but hides it.</summary>
    public bool IsVisible
    {
        get;
        internal set => SetProperty(ref field, value);
    } = true;

    internal void SyncItems(IReadOnlyList<IssueItemViewModel> desired) => CollectionSync.SyncInPlace(Items, desired);
}
