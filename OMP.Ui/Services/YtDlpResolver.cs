using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OMP.Ui.Helpers;
using OMP.Ui.Localization;
using OMP.Ui.Settings;

namespace OMP.Ui.Services;

internal sealed class YtDlpResolver(IUserSettingsService settings, ILogger<YtDlpResolver> logger) : IYtDlpResolver
{
    private static readonly TimeSpan _resolveTimeout = TimeSpan.FromSeconds(30);

    public async Task<YtDlpResolveResult> ResolveAsync(string pageUrl, CancellationToken cancellationToken)
    {
        var exePath = settings.Current.YtDlpPath is { Length: > 0 } configured ? configured : "yt-dlp";

        var startInfo = new ProcessStartInfo(exePath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-j");
        startInfo.ArgumentList.Add(pageUrl);

        using var process = new Process();
        process.StartInfo = startInfo;

        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
        {
            logger.LogWarning(ex, "yt-dlp executable not found at {ExePath}.", exePath);
            return YtDlpResolveResult.NotFound(pageUrl);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_resolveTimeout);

        var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            logger.LogWarning("yt-dlp timed out resolving {PageUrl}.", pageUrl);
            return YtDlpResolveResult.Failed(pageUrl, Strings.OpenUrl_TimeoutError);
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            logger.LogWarning(
                "yt-dlp exited with code {ExitCode} for {PageUrl}: {StdErr}", process.ExitCode, pageUrl, stderr);
            return YtDlpResolveResult.Failed(
                pageUrl, stderr is { Length: > 0 } ? stderr : Strings.OpenUrl_GenericResolveError);
        }

        using var document = JsonDocument.Parse(stdout);

        if (YtDlpFormatSelector.SelectMediaSources(document) is not { } selection)
        {
            return YtDlpResolveResult.Failed(pageUrl, Strings.OpenUrl_NoPlayableFormatError);
        }

        var subtitleSidecars = YtDlpSubtitleSelector.SelectSubtitleSidecars(document, settings.Current.Language);

        return YtDlpResolveResult.Success(
            pageUrl, selection.Url, selection.Title, selection.AudioSidecars, subtitleSidecars, selection.Headers);
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Already exited.
        }
    }
}
