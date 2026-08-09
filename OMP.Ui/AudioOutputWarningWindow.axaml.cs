using System;
using Avalonia.Controls;
using OMP.Ui.Localization;

namespace OMP.Ui;

public sealed partial class AudioOutputWarningWindow : Window
{
    public AudioOutputWarningWindow()
    {
        InitializeComponent();

        CloseButton.Click += (_, _) => Close();
    }

    public void Load(string reason)
    {
        GuidanceText.Text = OperatingSystem.IsLinux()
            ? Strings.AudioWarning_LinuxGuidance
            : Strings.AudioWarning_DefaultGuidance;

        ReasonText.Text = reason;
    }
}
