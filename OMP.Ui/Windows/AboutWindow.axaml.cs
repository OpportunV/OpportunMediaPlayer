using System.Diagnostics;
using Avalonia.Controls;
using OMP.Ui;
using OMP.Ui.Localization;

namespace OMP.Ui.Windows;

public sealed partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        AppNameText.Text = AppInfo.DisplayName;
        VersionText.Text = string.Format(Strings.About_VersionFormat, typeof(App).Assembly.GetName().Version?.ToString(3));

        GitHubButton.Click += (_, _) => Process.Start(new ProcessStartInfo(AppInfo.RepositoryUrl) { UseShellExecute = true });
        CloseButton.Click += (_, _) => Close();
    }
}
