namespace Kontext.Presentation;

public sealed class RepositoryFilterItemViewModel(string name, bool isVisible, Action<RepositoryFilterItemViewModel> changed)
{
    private bool _isVisible = isVisible;

    public string Name { get; } = name;

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible == value)
            {
                return;
            }

            _isVisible = value;
            changed(this);
        }
    }
}
