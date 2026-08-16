using System;
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

            var lockStream = TryAcquireLock(lockFilePath);

            if (lockStream is not null)
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

    public void Dispose()
    {
        _watcher?.Dispose();
        _lockStream?.Dispose();
    }

    private static FileStream? TryAcquireLock(string lockFilePath)
    {
        try
        {
            return new FileStream(lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException)
        {
            return null;
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
