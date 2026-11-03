using System.ComponentModel;
using System.Runtime.InteropServices;
using Kontext.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace Kontext.Views;

public sealed partial class SignInView : UserControl
{
    private const string CopyGlyph = "";
    private const string CopiedGlyph = "";

    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel),
        typeof(ShellViewModel),
        typeof(SignInView),
        new PropertyMetadata(null, OnViewModelChanged));

    public SignInView() => InitializeComponent();

    public ShellViewModel? ViewModel
    {
        get => (ShellViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private static void OnViewModelChanged(DependencyObject view, DependencyPropertyChangedEventArgs args)
    {
        var signIn = (SignInView)view;

        if (args.OldValue is ShellViewModel previous)
        {
            previous.PropertyChanged -= signIn.ViewModel_PropertyChanged;
        }

        if (args.NewValue is ShellViewModel current)
        {
            current.PropertyChanged += signIn.ViewModel_PropertyChanged;
        }

        signIn.Bindings?.Update();
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

    private async void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.VerificationUrl) && ViewModel?.VerificationUrl is { } url && LinkPolicy.IsSafeToOpen(url))
        {
            await Launcher.LaunchUriAsync(url);
        }
    }
}