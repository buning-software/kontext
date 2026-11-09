using Kontext.Presentation;
using Kontext.Terminal;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Kontext.Views;

public sealed partial class GeneralSessionView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel),
        typeof(GeneralSessionViewModel),
        typeof(GeneralSessionView),
        new PropertyMetadata(null, (view, _) => ((GeneralSessionView)view).Bindings?.Update()));

    public static readonly DependencyProperty ControlHostProperty = DependencyProperty.Register(
        nameof(ControlHost),
        typeof(TerminalControlHost),
        typeof(GeneralSessionView),
        new PropertyMetadata(null, (view, _) => ((GeneralSessionView)view).Bindings?.Update()));

    public GeneralSessionView() => InitializeComponent();

    public GeneralSessionViewModel? ViewModel
    {
        get => (GeneralSessionViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public TerminalControlHost? ControlHost
    {
        get => (TerminalControlHost?)GetValue(ControlHostProperty);
        set => SetValue(ControlHostProperty, value);
    }
}
