using System.Globalization;
using Kontext.Domain;
using Kontext.Presentation;
using Microsoft.UI.Xaml;

namespace Kontext.Views;

/// <summary>Small helpers that XAML function bindings call from <see cref="GitChangesView"/>.</summary>
public static class GitViewFormat
{
    public static string Number(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    public static Visibility VisibleWhenText(string? text) => string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;

    public static string CommitLabel(string? busyText) => busyText == GitChangesViewModel.CommittingText ? busyText : "Commit";

    public static string PushLabel(int unpushed, string? busyText)
        => busyText == GitChangesViewModel.PushingText ? busyText : unpushed > 0 ? $"Push ({unpushed})" : "Push";

    public static string Title(FileChange? change)
        => change is null
            ? string.Empty
            : change.OriginalPath is null ? change.Path : $"{change.OriginalPath} → {change.Path}";
}
