using System.Collections.ObjectModel;

namespace Kontext.Presentation;

public sealed class RepositoryGroupViewModel(string name, IEnumerable<IssueItemViewModel> items, bool isExpanded = true, Action<RepositoryGroupViewModel>? expandedChanged = null)
{
    private bool _isExpanded = isExpanded;

    public string Name { get; } = name;

    public ObservableCollection<IssueItemViewModel> Items { get; } = new(items);

    /// <summary>Set by the menu when the user expands or collapses the group; a change is reported so it can be remembered.</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
            {
                return;
            }

            _isExpanded = value;
            expandedChanged?.Invoke(this);
        }
    }
}
