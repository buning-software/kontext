using EasyWindowsTerminalControl;
using Kontext.Domain;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Kontext.Terminal;

public sealed class TerminalControlHost
{
    private Panel? _panel;

    public void Attach(Panel panel) => _panel = panel;

    public void Add(EasyTerminalControl control)
    {
        ArgumentNullException.ThrowIfNull(control);

        var panel = _panel ?? throw new TerminalStartException("The terminal area is not ready yet.");
        control.Visibility = Visibility.Collapsed;
        panel.Children.Add(control);
    }

    public void Remove(EasyTerminalControl control)
    {
        control.Visibility = Visibility.Collapsed;
        _panel?.Children.Remove(control);
    }

    public void Show(EasyTerminalControl? control)
    {
        if (_panel is null)
        {
            return;
        }

        foreach (var child in _panel.Children.Where(child => !ReferenceEquals(child, control)))
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
