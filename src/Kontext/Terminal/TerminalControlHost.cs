using EasyWindowsTerminalControl;
using Kontext.Domain;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Kontext.Terminal;

public sealed class TerminalControlHost
{
    private Panel? _panel;

    // Every surface (the issue pane and the general Claude pane) offers its panel. New controls start in any of them, collapsed,
    // and are moved into the panel of the surface that shows them, so the native terminal always sits where that surface is laid out.
    public void Attach(Panel panel) => _panel ??= panel;

    public void Add(EasyTerminalControl control)
    {
        ArgumentNullException.ThrowIfNull(control);

        var panel = _panel ?? throw new TerminalStartException("The terminal area is not ready yet.");
        control.Visibility = Visibility.Collapsed;
        panel.Children.Add(control);
    }

    public static void Remove(EasyTerminalControl control)
    {
        control.Visibility = Visibility.Collapsed;
        (control.Parent as Panel)?.Children.Remove(control);
    }

    public static void Show(EasyTerminalControl? control, Panel surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        if (control is not null && !ReferenceEquals(control.Parent, surface))
        {
            (control.Parent as Panel)?.Children.Remove(control);
            surface.Children.Add(control);
        }

        foreach (var child in surface.Children.Where(child => !ReferenceEquals(child, control)))
        {
            child.Visibility = Visibility.Collapsed;
        }

        if (control is not null)
        {
            control.Visibility = Visibility.Visible;
            FocusWhenReady(control);
        }
    }

    // A control that has only just become visible cannot take focus until it has been laid out, and the click that selected
    // the issue hands focus to the menu item after this runs. So focus once layout and that click have finished, and once more
    // when the control finishes loading if it still was not ready.
    private static void FocusWhenReady(EasyTerminalControl control)
    {
        if (!control.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => Focus(control)))
        {
            Focus(control);
        }
    }

    private static void Focus(EasyTerminalControl control)
    {
        if (control.Visibility != Visibility.Visible)
        {
            return;
        }

        control.UpdateLayout();
        if (control.Focus(FocusState.Programmatic) || control.IsLoaded)
        {
            return;
        }

        void OnLoaded(object sender, RoutedEventArgs args)
        {
            control.Loaded -= OnLoaded;
            if (control.Visibility == Visibility.Visible)
            {
                control.Focus(FocusState.Programmatic);
            }
        }

        control.Loaded += OnLoaded;
    }
}
