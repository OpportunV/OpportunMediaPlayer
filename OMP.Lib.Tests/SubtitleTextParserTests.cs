using OMP.Lib.Subtitle;

namespace OMP.Lib.Tests;

public class SubtitleTextParserTests
{
    [Fact]
    public void Parse_PlainText_ReturnsSingleUnstyledRun()
    {
        var lines = SubtitleTextParser.Parse("Hello world");

        var line = Assert.Single(lines);
        var run = Assert.Single(line.Runs);
        Assert.Equal("Hello world", run.Text);
        Assert.False(run.Bold);
        Assert.False(run.Italic);
    }

    [Fact]
    public void Parse_HardLineBreak_SplitsIntoTwoLines()
    {
        var lines = SubtitleTextParser.Parse(@"Line one\NLine two");

        Assert.Equal(2, lines.Count);
        Assert.Equal("Line one", Assert.Single(lines[0].Runs).Text);
        Assert.Equal("Line two", Assert.Single(lines[1].Runs).Text);
    }

    [Fact]
    public void Parse_BoldOverrideTags_ToggleBoldOnRuns()
    {
        var lines = SubtitleTextParser.Parse(@"{\b1}bold{\b0}normal");

        var runs = Assert.Single(lines).Runs;
        Assert.Equal(2, runs.Count);
        Assert.Equal("bold", runs[0].Text);
        Assert.True(runs[0].Bold);
        Assert.Equal("normal", runs[1].Text);
        Assert.False(runs[1].Bold);
    }

    [Fact]
    public void Parse_ItalicOverrideTags_ToggleItalicOnRuns()
    {
        var lines = SubtitleTextParser.Parse(@"{\i1}italic{\i0}normal");

        var runs = Assert.Single(lines).Runs;
        Assert.Equal(2, runs.Count);
        Assert.True(runs[0].Italic);
        Assert.False(runs[1].Italic);
    }

    [Fact]
    public void Parse_UnhandledOverrideTags_AreStrippedWithNoEffect()
    {
        var lines = SubtitleTextParser.Parse(@"{\pos(400,570)\c&H00FF00&\fs20}text");

        var run = Assert.Single(Assert.Single(lines).Runs);
        Assert.Equal("text", run.Text);
        Assert.False(run.Bold);
        Assert.False(run.Italic);
    }

    [Fact]
    public void Parse_DrawingModeBlock_SuppressesContainedTextEntirely()
    {
        var lines = SubtitleTextParser.Parse(@"{\p1}m 0 0 l 100 0 100 100{\p0}visible");

        var run = Assert.Single(Assert.Single(lines).Runs);
        Assert.Equal("visible", run.Text);
    }

    [Fact]
    public void Parse_EmptyText_ReturnsSingleEmptyLine()
    {
        var lines = SubtitleTextParser.Parse(string.Empty);

        var line = Assert.Single(lines);
        Assert.Empty(line.Runs);
    }

    [Fact]
    public void Parse_ConsecutiveRunsWithSameStyle_AreMerged()
    {
        var lines = SubtitleTextParser.Parse(@"{\pos(1,1)}foo{\c&HFFFFFF&}bar");

        var run = Assert.Single(Assert.Single(lines).Runs);
        Assert.Equal("foobar", run.Text);
    }
}
