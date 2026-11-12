using Kontext.Contracts;
using Kontext.Domain;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Kontext.Services;

internal sealed class ContentDialogEnterprisePrompt(XamlRootProvider root) : IEnterpriseServerPrompt
{
    private string _lastAddress = string.Empty;

    public async Task<EnterpriseCredentials?> AskAsync()
    {
        var address = new TextBox { Header = "Server address", PlaceholderText = "yourcompany.ghe.com", Text = _lastAddress };
        var token = new PasswordBox { Header = "Personal access token" };
        var error = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed,
            Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
        };

        var hint = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Text = "Create a personal access token on your GitHub Enterprise server (Settings, Developer settings, Personal access tokens) with the repo and workflow scopes, then paste it here. Kontext stores it in the Windows Credential Locker.",
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };

        var dialog = new ContentDialog
        {
            XamlRoot = root.Current,
            Title = "Sign in with GitHub Enterprise",
            Content = new StackPanel { Spacing = 12, Children = { hint, address, token, error } },
            PrimaryButtonText = "Sign in",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };

        GitHubServer? server = null;
        dialog.PrimaryButtonClick += (_, args) =>
        {
            var valid = GitHubServer.TryCreate(address.Text, out server, out var message);
            if (valid && string.IsNullOrWhiteSpace(token.Password))
            {
                valid = false;
                message = "Paste your personal access token.";
            }

            if (valid)
            {
                return;
            }

            error.Text = message;
            error.Visibility = Visibility.Visible;
            args.Cancel = true;
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary || server is null)
        {
            return null;
        }

        _lastAddress = address.Text.Trim();
        return new EnterpriseCredentials(server, token.Password.Trim());
    }
}
