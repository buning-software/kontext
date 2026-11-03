using System.Runtime.InteropServices;
using Kontext.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace Kontext.Views;

public sealed partial class DeviceCodePanel : UserControl
{
    private const string CopyGlyph = "";
    private const string CopiedGlyph = "";

    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel),
        typeof(ShellViewModel),
        typeof(DeviceCodePanel),
        new PropertyMetadata(null, (panel, _) => ((DeviceCodePanel)panel).Bindings?.Update()));

    public DeviceCodePanel() => InitializeComponent();

    public ShellViewModel? ViewModel
    {
        get => (ShellViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private async void CopyCodeButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(ViewModel?.UserCode))
        {
            return;
        }

        try
        {
            var package = new DataPackage();
            package.SetText(ViewModel.UserCode);
            Clipboard.SetContent(package);
            Clipboard.Flush();
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            return;
        }

        CopyCodeIcon.Glyph = CopiedGlyph;
        await Task.Delay(TimeSpan.FromSeconds(1.5));
        CopyCodeIcon.Glyph = CopyGlyph;
    }
}
