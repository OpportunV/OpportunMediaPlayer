using System;
using System.IO;
using System.Linq;
using OMP.Lib;

namespace OMP.Ui.Services;

internal static class FFmpegLibraryLocator
{
    private static readonly string[] _candidateDirectories =
    [
        "/opt/homebrew/opt/ffmpeg@7/lib",
        "/usr/local/opt/ffmpeg@7/lib"
    ];

    private const string ProbeFileName = "libavcodec.61.dylib";

    public static NativeLibraryOptions CreateOptions() => new()
    {
        FFmpegLibraryDirectory = OperatingSystem.IsMacOS() ? LocateMacLibraryDirectory() : null
    };

    private static string? LocateMacLibraryDirectory()
    {
        return _candidateDirectories.FirstOrDefault(directory => File.Exists(Path.Combine(directory, ProbeFileName)));
    }
}
