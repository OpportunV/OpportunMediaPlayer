using OMP.Lib.Audio;
using OMP.Lib.Audio.Output;

namespace OMP.Lib.Tests;

public class AudioRouteMatcherTests
{
    [Fact]
    public void Match_ExactTitleAndLanguage_RoutesToTheSavedOutput()
    {
        var streams = new[]
        {
            new AudioStream(0, "aac", "Commentary", "eng"),
            new AudioStream(1, "aac", "Main", "rus")
        };
        var outputs = new[] { new AudioOutput(1, "Speakers"), new AudioOutput(2, "Headset") };
        var preferred = new[] { new PreferredAudioTrack("Headset", "Main", "rus") };

        var routes = AudioRouteMatcher.Match(streams, outputs, preferred);

        var route = Assert.Single(routes);
        Assert.Equal("Main", route.Stream.Title);
        Assert.Equal("Headset", route.Output.FriendlyName);
    }

    [Fact]
    public void Match_NoTitleMatch_FallsBackToLanguageOnly()
    {
        var streams = new[] { new AudioStream(0, "aac", "Director's Cut", "rus") };
        var outputs = new[] { new AudioOutput(1, "Speakers") };
        var preferred = new[] { new PreferredAudioTrack("Speakers", "Main", "rus") };

        var routes = AudioRouteMatcher.Match(streams, outputs, preferred);

        var route = Assert.Single(routes);
        Assert.Equal("rus", route.Stream.Language);
    }

    [Fact]
    public void Match_UnknownTitle_NeverMatchesByTitle()
    {
        var streams = new[] { new AudioStream(0, "aac", "Unknown", "eng") };
        var outputs = new[] { new AudioOutput(1, "Speakers") };
        var preferred = new[] { new PreferredAudioTrack("Speakers", "Unknown", "eng") };

        var routes = AudioRouteMatcher.Match(streams, outputs, preferred);

        var route = Assert.Single(routes);
        Assert.Equal("eng", route.Stream.Language);
    }

    [Fact]
    public void Match_UnknownLanguage_NeverMatchesByLanguage()
    {
        var streams = new[]
        {
            new AudioStream(0, "aac", "Track A", "Unknown"),
            new AudioStream(1, "aac", "Track B", "und")
        };
        var outputs = new[] { new AudioOutput(1, "Speakers") };
        var preferred = new[] { new PreferredAudioTrack("Speakers", "Unrelated", "Unknown") };

        var routes = AudioRouteMatcher.Match(streams, outputs, preferred);

        var route = Assert.Single(routes);
        Assert.Equal(0, route.Stream.Id);
    }

    [Fact]
    public void Match_PartialMatch_FillsRemainingOutputsInOrder()
    {
        var streams = new[]
        {
            new AudioStream(0, "aac", "Main", "eng"),
            new AudioStream(1, "aac", "Commentary", "eng")
        };
        var outputs = new[] { new AudioOutput(1, "Speakers"), new AudioOutput(2, "Headset") };
        var preferred = new[]
        {
            new PreferredAudioTrack("Speakers", "Main", "eng"),
            new PreferredAudioTrack("Headset", "Nonexistent", "rus")
        };

        var routes = AudioRouteMatcher.Match(streams, outputs, preferred);

        Assert.Equal(2, routes.Count);
        Assert.Contains(routes, r => r.Output.FriendlyName == "Speakers" && r.Stream.Title == "Main");
        Assert.Contains(routes, r => r.Output.FriendlyName == "Headset" && r.Stream.Title == "Commentary");
    }

    [Fact]
    public void Match_NoMatchesAtAll_FillsEverythingInOrder()
    {
        var streams = new[] { new AudioStream(0, "aac", "Main", "eng"), new AudioStream(1, "aac", "Alt", "rus") };
        var outputs = new[] { new AudioOutput(1, "Speakers"), new AudioOutput(2, "Headset") };
        var preferred = new[]
        {
            new PreferredAudioTrack("Speakers", "Nothing", "jpn"),
            new PreferredAudioTrack("Headset", "Nothing Else", "kor")
        };

        var routes = AudioRouteMatcher.Match(streams, outputs, preferred);

        Assert.Equal(2, routes.Count);
        Assert.Contains(routes, r => r.Output.FriendlyName == "Speakers" && r.Stream.Id == 0);
        Assert.Contains(routes, r => r.Output.FriendlyName == "Headset" && r.Stream.Id == 1);
    }

    [Fact]
    public void Match_ThreeLetterLanguageAgainstBcp47_MatchesAcrossLocalAndWebSourceTagging()
    {
        var streams = new[] { new AudioStream(0, "aac", "Unknown", "eng") };
        var outputs = new[] { new AudioOutput(1, "Speakers") };
        var preferred = new[] { new PreferredAudioTrack("Speakers", "Unknown", "en-US") };

        var routes = AudioRouteMatcher.Match(streams, outputs, preferred);

        var route = Assert.Single(routes);
        Assert.Equal("eng", route.Stream.Language);
    }

    [Fact]
    public void Match_Bcp47LanguageAgainstThreeLetterPreference_MatchesAcrossLocalAndWebSourceTagging()
    {
        var streams = new[] { new AudioStream(0, "aac", "Unknown", "fr-FR") };
        var outputs = new[] { new AudioOutput(1, "Speakers") };
        var preferred = new[] { new PreferredAudioTrack("Speakers", "Unknown", "fra") };

        var routes = AudioRouteMatcher.Match(streams, outputs, preferred);

        var route = Assert.Single(routes);
        Assert.Equal("fr-FR", route.Stream.Language);
    }

    [Fact]
    public void Match_SavedOutputNoLongerPresent_IsSkipped()
    {
        var streams = new[] { new AudioStream(0, "aac", "Main", "eng") };
        var outputs = new[] { new AudioOutput(1, "Speakers") };
        var preferred = new[] { new PreferredAudioTrack("Unplugged Headset", "Main", "eng") };

        var routes = AudioRouteMatcher.Match(streams, outputs, preferred);

        Assert.Empty(routes);
    }
}
