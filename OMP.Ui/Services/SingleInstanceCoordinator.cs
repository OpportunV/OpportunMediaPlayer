using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Avalonia.Threading;
using Serilog;

namespace OMP.Ui.Services;

public sealed class SingleInstanceCoordinator : IDisposable
{
    public bool HandedOff { get; }

    private readonly string _requestFilePath;
    private readonly FileStream? _lockStream;
    private FileSystemWatcher? _watcher;

    private const string LockFileName = "single-instance.lock";
    private const string RequestFileName = "open-request.signal";

    private SingleInstanceCoordinator(FileStream? lockStream, string requestFilePath, bool handedOff)
    {
        _lockStream = lockStream;
        _requestFilePath = requestFilePath;
        HandedOff = handedOff;
    }

    public static SingleInstanceCoordinator AcquireOrHandOff(string? filePathToOpen)
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppInfo.DirectoryName);
        var lockFilePath = Path.Combine(directory, LockFileName);
        var requestFilePath = Path.Combine(directory, RequestFileName);

        try
        {
            Directory.CreateDirectory(directory);


            if (TryAcquireLock(lockFilePath, out var lockStream))
            {
                DeleteRequestFile(requestFilePath);
                return new SingleInstanceCoordinator(lockStream, requestFilePath, handedOff: false);
            }

            File.WriteAllText(requestFilePath, filePathToOpen ?? string.Empty);
            Log.Information("Another instance is already running; handed the open request off to it.");
            return new SingleInstanceCoordinator(null, requestFilePath, handedOff: true);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Single-instance coordination failed; opening a new window instead.");
            return new SingleInstanceCoordinator(null, requestFilePath, handedOff: false);
        }
    }

    public void StartWatchingForOpenRequests(Action<string?> onOpenRequested)
    {
        if (_lockStream is null)
        {
            return;
        }

        var directory = Path.GetDirectoryName(_requestFilePath)!;
        _watcher = new FileSystemWatcher(directory, Path.GetFileName(_requestFilePath))
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
        };

        FileSystemEventHandler handler = (_, _) =>
        {
            if (!TryReadAndDeleteRequestFile(_requestFilePath, out var path))
            {
                return;
            }

            Dispatcher.UIThread.Post(() => onOpenRequested(path));
        };

        _watcher.Created += handler;
        _watcher.Changed += handler;
        _watcher.EnableRaisingEvents = true;
    }

    /// <summary>
    /// Gives up this process's claim to being the single instance, without waiting for shutdown.
    /// Restarting must do this *before* spawning the replacement: the lock is held until this
    /// instance's window closes, so a replacement launched first would find the lock still taken,
    /// hand its request back to the process that is already going away, and exit without a window.
    /// Safe to call more than once - <see cref="Dispose"/> repeats it.
    /// </summary>
    public void ReleaseLock()
    {
        _watcher?.Dispose();
        _watcher = null;
        _lockStream?.Dispose();
    }

    public void Dispose() => ReleaseLock();

    private static bool TryAcquireLock(string lockFilePath, [MaybeNullWhen(false)] out FileStream lockStream)
    {
        try
        {
            lockStream = new FileStream(lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            lockStream = null;
            return false;
        }
    }

    private static bool TryReadAndDeleteRequestFile(string requestFilePath, out string? filePathToOpen)
    {
        try
        {
            var content = File.ReadAllText(requestFilePath);
            filePathToOpen = string.IsNullOrEmpty(content) ? null : content;
            DeleteRequestFile(requestFilePath);
            return true;
        }
        catch (IOException)
        {
            filePathToOpen = null;
            return false;
        }
    }

    private static void DeleteRequestFile(string requestFilePath)
    {
        try
        {
            File.Delete(requestFilePath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}