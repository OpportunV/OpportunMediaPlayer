using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using OMP.Ui.Localization;
using OMP.Ui.Services;

namespace OMP.Ui.Windows;

public sealed partial class OpenUrlWindow : Window
{
    private readonly IYtDlpResolver _resolver;

    public OpenUrlWindow(IYtDlpResolver resolver)
    {
        _resolver = resolver;

        InitializeComponent();

        CancelButton.Click += (_, _) => Close(null);
        OkButton.Click += async (_, _) => await OnOkClick();
        UrlTextBox.KeyDown += async (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                await OnOkClick();
            }
        };
    }

    public void Load(string? initialUrl)
    {
        UrlTextBox.Text = initialUrl ?? string.Empty;
    }

    private async Task OnOkClick()
    {
        var url = UrlTextBox.Text?.Trim();

        if (string.IsNullOrEmpty(url) || !LooksLikeUrl(url))
        {
            ShowInlineError(Strings.OpenUrl_InvalidUrlError);
            return;
        }

        HideInlineError();
        SetBusy(true);

        var result = await _resolver.ResolveAsync(url, CancellationToken.None);

        SetBusy(false);

        var errorMessage = result.Match(
            onSuccess: (_, _, _) => (string?)null,
            onNotFound: YtDlpNotFoundMessage,
            onFailed: message => message);

        if (errorMessage is null)
        {
            Close(result);
            return;
        }

        ShowInlineError(errorMessage);
    }

    private static string YtDlpNotFoundMessage() =>
        OperatingSystem.IsWindows() ? Strings.OpenFileError_YtDlpNotFoundWindowsHeading
        : OperatingSystem.IsMacOS() ? Strings.OpenFileError_YtDlpNotFoundMacHeading
        : Strings.OpenFileError_YtDlpNotFoundLinuxHeading;

    private void ShowInlineError(string message)
    {
        ErrorText.Text = message;
        ErrorText.IsVisible = true;
    }

    private void HideInlineError()
    {
        ErrorText.IsVisible = false;
    }

    private void SetBusy(bool isBusy)
    {
        UrlTextBox.IsEnabled = !isBusy;
        OkButton.IsEnabled = !isBusy;
        CancelButton.IsEnabled = !isBusy;
        BusyPanel.IsVisible = isBusy;
    }

    private static bool LooksLikeUrl(string text) =>
        Uri.TryCreate(text, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
