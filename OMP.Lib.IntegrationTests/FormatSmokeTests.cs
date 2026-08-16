using Microsoft.Extensions.Logging.Abstractions;
using OMP.Lib.Session;

namespace OMP.Lib.IntegrationTests;

public sealed class FormatSmokeTests
{
    [Theory]
    [MemberData(nameof(TestFixtures.AllFormats), MemberType = typeof(TestFixtures))]
    public void Open_EveryTestFixtureFormat_OpensWithoutThrowing(string filePath)
    {
        var registry = new MediaSessionRegistry(
            new PlaybackTuningOptions(),
            NullLoggerFactory.Instance,
            NativeLibraryOptionsFactory.Create());

        try
        {
            registry.Open(filePath);

            Assert.True(registry.Current!.Duration > TimeSpan.Zero);
        }
        finally
        {
            registry.Close();
        }
    }
}
