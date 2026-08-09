using System.Diagnostics;
using Avalonia.Controls;

namespace OMP.Ui;

public sealed partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        AppNameText.Text = AppInfo.DisplayName;
        VersionText.Text = $"Version {typeof(App).Assembly.GetName().Version?.ToString(3)}";

        GitHubButton.Click += (_, _) => Process.Start(new ProcessStartInfo(AppInfo.RepositoryUrl) { UseShellExecute = true });
        CloseButton.Click += (_, _) => Close();
    }
}
