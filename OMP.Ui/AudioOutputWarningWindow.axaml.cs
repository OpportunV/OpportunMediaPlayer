using System;
using Avalonia.Controls;

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
            ? "This usually means a system audio library is missing. Try installing JACK's " +
              "shared library, then restart the app:\n\nsudo apt install libjack-jackd2-0"
            : "Playback will continue without audio output.";

        ReasonText.Text = reason;
    }
}
