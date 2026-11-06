using System.ComponentModel;
using System.Globalization;
using Kontext.Domain;

namespace Kontext.Presentation;

/// <summary>One line of the change list: either a group header ("Staged (2)") or a changed file.</summary>
public sealed record GitChangeRow(string Text, FileChange? Change) : INotifyPropertyChanged
{
    private bool _isChecked;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsHeader => Change is null;

    /// <summary>Whether the file is included in the next commit. Called by the list when the user toggles it.</summary>
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value)
            {
                return;
            }

            _isChecked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
            Toggled?.Invoke(this, value);
        }
    }

    /// <summary>Starts the row checked without reporting a toggle.</summary>
    public bool InitiallyChecked
    {
        init => _isChecked = value;
    }

    /// <summary>Told when the user checks or unchecks the row.</summary>
    public Action<GitChangeRow, bool>? Toggled { get; init; }

    public string StatusLetter => Change?.Status switch
    {
        FileChangeStatus.Modified => "M",
        // A new file is "+", staged or not.
        FileChangeStatus.Added or FileChangeStatus.Untracked => "+",
        FileChangeStatus.Deleted => "D",
        FileChangeStatus.Renamed => "R",
        FileChangeStatus.Conflicted => "!",
        _ => string.Empty,
    };

    public string FileName => Change is null ? string.Empty : Change.Path[(Change.Path.LastIndexOf('/') + 1)..];

    public string Folder => Change is null ? string.Empty : Change.Path[..Math.Max(Change.Path.LastIndexOf('/'), 0)];

    public bool HasCounts => Change is { Additions: not null } or { Deletions: not null };

    public string AdditionsText => Change?.Additions is { } additions ? string.Create(CultureInfo.InvariantCulture, $"+{additions}") : string.Empty;

    public string DeletionsText => Change?.Deletions is { } deletions ? string.Create(CultureInfo.InvariantCulture, $"−{deletions}") : string.Empty;

    /// <summary>Sets the checked state from the view model, so the box follows without reporting a toggle.</summary>
    internal void SetCheckedFromModel(bool value)
    {
        if (_isChecked != value)
        {
            _isChecked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
        }
    }
}
