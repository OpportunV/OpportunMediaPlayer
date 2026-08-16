using System;
using System.IO;
using System.Linq;

namespace OMP.Lib.IntegrationTests;

internal static class NativeLibraryOptionsFactory
{
    private static readonly string[] _macCandidateDirectories =
    [
        "/opt/homebrew/opt/ffmpeg@7/lib",
        "/usr/local/opt/ffmpeg@7/lib"
    ];

    private const string ProbeFileName = "libavcodec.61.dylib";

    public static NativeLibraryOptions Create() => new()
    {
        FFmpegLibraryDirectory = OperatingSystem.IsMacOS() ? LocateMacLibraryDirectory() : null
    };

    private static string? LocateMacLibraryDirectory()
    {
        return _macCandidateDirectories.FirstOrDefault(directory => File.Exists(Path.Combine(directory, ProbeFileName)));
    }
}
