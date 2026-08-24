using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using OMP.Ui.Services;

namespace OMP.Ui.Helpers;

internal static class ApplicationRestart
{
    /// <summary>
    /// Relaunches the app and shuts the current instance down, optionally reopening
    /// <paramref name="resumeFilePath"/>. Reads APPIMAGE first: on Linux an AppImage's
    /// <see cref="Environment.ProcessPath"/> points inside the extracted mount, not at the
    /// distributable, so relaunching from it would fail once the mount is gone.
    /// <para>
    /// <paramref name="singleInstance"/> is released before the replacement is spawned, and is a
    /// parameter rather than the caller's responsibility because getting that order wrong makes
    /// the replacement exit silently as a duplicate instance.
    /// </para>
    /// </summary>
    public static void Restart(string? resumeFilePath, SingleInstanceCoordinator singleInstance)
    {
        var exePath = Environment.GetEnvironmentVariable("APPIMAGE") ?? Environment.ProcessPath;

        if (exePath is not null)
        {
            var startInfo = new ProcessStartInfo(exePath);

            if (resumeFilePath is not null)
            {
                startInfo.ArgumentList.Add(resumeFilePath);
            }

            singleInstance.ReleaseLock();
            Process.Start(startInfo);
        }

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
        {
            lifetime.Shutdown();
        }
    }
}
