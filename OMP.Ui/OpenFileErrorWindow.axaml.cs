using Avalonia.Controls;

namespace OMP.Ui;

public sealed partial class OpenFileErrorWindow : Window
{
    public OpenFileErrorWindow()
    {
        InitializeComponent();

        CloseButton.Click += (_, _) => Close();
    }

    public void Load(string reason)
    {
        ReasonText.Text = reason;
    }
}
