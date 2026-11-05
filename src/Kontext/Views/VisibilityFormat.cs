using Microsoft.UI.Xaml;

namespace Kontext.Views;

/// <summary>Visibility helpers that XAML function bindings call.</summary>
public static class VisibilityFormat
{
    public static Visibility VisibleWhen(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility VisibleWhen(bool value, bool also) => value && also ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility VisibleWhenNot(bool value, bool not) => value && !not ? Visibility.Visible : Visibility.Collapsed;

    public static bool AndNot(bool value, bool not) => value && !not;

    public static bool Not(bool value) => !value;
}
