namespace OMP.Lib.IntegrationTests;

internal static class TestFixtures
{
    public static string VideoWithAudioMp4 => Path.Combine(FixturesRoot, "video", "sample.mp4");

    public static TheoryData<string> AllFormats => new(AllFormatPaths);

    private static string FixturesRoot => Path.Combine(AppContext.BaseDirectory, "test-fixtures");

    private static IEnumerable<string> AllFormatPaths => Directory
        .EnumerateFiles(Path.Combine(FixturesRoot, "video"))
        .Concat(Directory.EnumerateFiles(Path.Combine(FixturesRoot, "audio")));
}
