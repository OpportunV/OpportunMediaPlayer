using Microsoft.Extensions.Logging.Abstractions;
using OMP.Lib.Session;
using OMP.Lib.Subtitle;

namespace OMP.Lib.IntegrationTests;

/// <summary>
/// Covers subtitles generalized to the same multi-source architecture audio sidecars already use
/// (see CLAUDE.md) - a subtitle sidecar reaching the session either via <see cref="MediaOpenRequest"/>
/// (the lazy, pending-until-routed path a web source's caption tracks will use) or via
/// <see cref="IMediaSession.AddSubtitleSidecar"/> (the eager, attach-to-a-running-session path a
/// local file picker uses). Each test opens its own session (not the shared
/// <see cref="MultiSourceSessionFixture"/>) so routing/seek order is fully controlled.
/// </summary>
public sealed class SubtitleSidecarTests
{
    private const string ZoneId = "zone-1";
    private const int SettleMs = 50;
    private const int PlaybackSettleMs = 800;

    [Fact]
    public void Open_WithSubtitleSidecar_CatalogIncludesPendingEntryWithSuppliedMetadata()
    {
        var registry = CreateRegistryWithSubtitleSidecarRequest();

        try
        {
            var stream = Assert.Single(registry.Current!.SubtitleStreams);
            Assert.Equal("English", stream.Title);
            Assert.Equal("en", stream.Language);
            Assert.True(stream.IsTextBased);
        }
        finally
        {
            registry.Close();
        }
    }

    [Fact]
    public void SetSubtitleRoutes_RoutingPendingSidecar_OpensLazilyAndProducesCues()
    {
        var registry = CreateRegistryWithSubtitleSidecarRequest();

        try
        {
            var session = registry.Current!;
            var stream = session.SubtitleStreams[0];

            session.SetSubtitleRoutes([new SubtitleRoute(stream, ZoneId)]);
            Thread.Sleep(SettleMs);

            session.Play();
            Thread.Sleep(PlaybackSettleMs);

            var cues = session.GetActiveSubtitleCues();
            Assert.Contains(cues, c => c.Lines.Any(l => l.Runs.Any(r => r.Text.Contains("First test caption"))));
        }
        finally
        {
            registry.Close();
        }
    }

    [Fact]
    public void AddSubtitleSidecar_ToRunningSessionWithNoSubtitles_AddsNewCatalogEntryAndCanBeRouted()
    {
        var registry = new MediaSessionRegistry(
            new PlaybackTuningOptions(), NullLoggerFactory.Instance, NativeLibraryOptionsFactory.Create());

        try
        {
            registry.Open(TestFixtures.VideoWithAudioMp4);
            var session = registry.Current!;

            Assert.Empty(session.SubtitleStreams);

            var added = session.AddSubtitleSidecar(new SubtitleSidecarSource(TestFixtures.SubtitleSidecarSrt, Title: "External"));

            Assert.Equal("External", added.Title);
            Assert.True(added.IsTextBased);
            Assert.Contains(session.SubtitleStreams, s => s.Id == added.Id);

            session.SetSubtitleRoutes([new SubtitleRoute(added, ZoneId)]);
            Thread.Sleep(SettleMs);

            session.Play();
            Thread.Sleep(PlaybackSettleMs);

            var cues = session.GetActiveSubtitleCues();
            Assert.Contains(cues, c => c.Lines.Any(l => l.Runs.Any(r => r.Text.Contains("First test caption"))));
        }
        finally
        {
            registry.Close();
        }
    }

    [Fact]
    public void Seek_AfterRoutingSubtitleOnlySidecar_RepositionsThatSourceWithoutThrowing()
    {
        var registry = CreateRegistryWithSubtitleSidecarRequest();

        try
        {
            var session = registry.Current!;
            var stream = session.SubtitleStreams[0];
            session.SetSubtitleRoutes([new SubtitleRoute(stream, ZoneId)]);
            Thread.Sleep(SettleMs);

            var target = TimeSpan.FromSeconds(8);
            session.Seek(target);
            session.Play();
            Thread.Sleep(PlaybackSettleMs);

            var cues = session.GetActiveSubtitleCues();
            Assert.Contains(cues, c => c.Lines.Any(l => l.Runs.Any(r => r.Text.Contains("Third and final"))));
        }
        finally
        {
            registry.Close();
        }
    }

    [Fact]
    public void SetSubtitleRoutes_RetryAfterFailedSidecarOpen_SucceedsOnceTheSourceBecomesAvailable()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"omp-subtitle-retry-{Guid.NewGuid():N}.srt");

        var registry = new MediaSessionRegistry(
            new PlaybackTuningOptions(), NullLoggerFactory.Instance, NativeLibraryOptionsFactory.Create());

        try
        {
            registry.Open(
                new MediaOpenRequest(
                    TestFixtures.VideoWithAudioMp4,
                    [],
                    SubtitleSidecars: [new SubtitleSidecarSource(missingPath, Title: "Retry")]));

            var session = registry.Current!;
            var stream = session.SubtitleStreams[0];

            session.SetSubtitleRoutes([new SubtitleRoute(stream, ZoneId)]);
            Thread.Sleep(SettleMs);
            Assert.Empty(session.SubtitleRoutes);

            File.Copy(TestFixtures.SubtitleSidecarSrt, missingPath);

            session.SetSubtitleRoutes([new SubtitleRoute(stream, ZoneId)]);
            Thread.Sleep(SettleMs);
            Assert.Single(session.SubtitleRoutes);
        }
        finally
        {
            registry.Close();
            File.Delete(missingPath);
        }
    }

    [Fact]
    public void SetSubtitleRoutes_MixedValidAndUnresolvableRoutes_ReturnsOnlyTheAppliedOnes()
    {
        var registry = new MediaSessionRegistry(
            new PlaybackTuningOptions(), NullLoggerFactory.Instance, NativeLibraryOptionsFactory.Create());

        try
        {
            registry.Open(TestFixtures.VideoWithAudioMp4);
            var session = registry.Current!;

            var valid = session.AddSubtitleSidecar(new SubtitleSidecarSource(TestFixtures.SubtitleSidecarSrt, Title: "Valid"));

            var unresolvable = new SubtitleStream(9999, "Unknown", "Unresolvable", "Unknown", IsTextBased: true);

            var applied = session.SetSubtitleRoutes(
            [
                new SubtitleRoute(valid, "zone-a"),
                new SubtitleRoute(unresolvable, "zone-b")
            ]);

            var route = Assert.Single(applied);
            Assert.Equal(valid.Id, route.Stream.Id);
            Assert.Equal(applied, session.SubtitleRoutes);
        }
        finally
        {
            registry.Close();
        }
    }

    [Fact]
    public void SetSubtitleRoutes_TwoSubtitleSidecars_EachZoneReceivesOnlyItsOwnSourcesCues()
    {
        var secondPath = Path.Combine(Path.GetTempPath(), $"omp-subtitle-second-{Guid.NewGuid():N}.srt");
        File.WriteAllText(
            secondPath,
            """
            1
            00:00:00,500 --> 00:00:03,000
            Alternate track caption.

            2
            00:00:04,000 --> 00:00:07,000
            Alternate track, second caption.

            """);

        var registry = new MediaSessionRegistry(
            new PlaybackTuningOptions(), NullLoggerFactory.Instance, NativeLibraryOptionsFactory.Create());

        try
        {
            registry.Open(TestFixtures.VideoWithAudioMp4);
            var session = registry.Current!;

            var first = session.AddSubtitleSidecar(new SubtitleSidecarSource(TestFixtures.SubtitleSidecarSrt, Title: "First"));
            var second = session.AddSubtitleSidecar(new SubtitleSidecarSource(secondPath, Title: "Second"));

            session.SetSubtitleRoutes([new SubtitleRoute(first, "zone-a"), new SubtitleRoute(second, "zone-b")]);
            Thread.Sleep(SettleMs);

            session.Play();
            Thread.Sleep(PlaybackSettleMs);

            var cues = session.GetActiveSubtitleCues();

            Assert.Contains(cues, c => c.ZoneId == "zone-a" && ContainsText(c, "First test caption"));
            Assert.Contains(cues, c => c.ZoneId == "zone-b" && ContainsText(c, "Alternate track caption"));

            Assert.DoesNotContain(cues, c => c.ZoneId == "zone-a" && ContainsText(c, "Alternate track"));
            Assert.DoesNotContain(cues, c => c.ZoneId == "zone-b" && ContainsText(c, "First test caption"));
        }
        finally
        {
            registry.Close();
            File.Delete(secondPath);
        }
    }

    private static bool ContainsText(SubtitleCue cue, string text) =>
        cue.Lines.Any(l => l.Runs.Any(r => r.Text.Contains(text)));

    private static IMediaSessionRegistry CreateRegistryWithSubtitleSidecarRequest()
    {
        var registry = new MediaSessionRegistry(
            new PlaybackTuningOptions(), NullLoggerFactory.Instance, NativeLibraryOptionsFactory.Create());

        registry.Open(
            new MediaOpenRequest(
                TestFixtures.VideoWithAudioMp4,
                [],
                SubtitleSidecars: [new SubtitleSidecarSource(TestFixtures.SubtitleSidecarSrt, Language: "en", Title: "English")]));

        return registry;
    }
}
