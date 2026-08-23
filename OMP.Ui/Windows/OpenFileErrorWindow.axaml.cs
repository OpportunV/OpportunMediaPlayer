using Avalonia.Controls;

namespace OMP.Ui.Windows;

public sealed partial class OpenFileErrorWindow : Window
{
    public OpenFileErrorWindow()
    {
        InitializeComponent();

        CloseButton.Click += (_, _) => Close();
    }

    public void Load(string heading, string reason)
    {
        HeadingText.Text = heading;
        ReasonText.Text = reason;
        TechnicalDetailLabel.IsVisible = ReasonText.IsVisible = !string.IsNullOrEmpty(reason);
    }
}
