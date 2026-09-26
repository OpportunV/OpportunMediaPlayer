using Avalonia.Headless.XUnit;
using OMP.Lib.Subtitle;
using OMP.Ui.Controls;
using OMP.Ui.Models;

namespace OMP.Ui.Tests.Controls;

public class SubtitleTrackPickerTests
{
    private static readonly SubtitleStreamOption _english =
        new(new SubtitleStream(1, "Unknown", "English (auto-generated)", "en", IsTextBased: true));

    private static readonly SubtitleStreamOption _russian =
        new(new SubtitleStream(2, "Unknown", "Русский (auto-generated, translated)", "ru", IsTextBased: true));

    private static readonly SubtitleStreamOption _bitmap =
        new(new SubtitleStream(3, "hdmv_pgs_subtitle", "Signs", "en", IsTextBased: false));

    [AvaloniaFact]
    public void Filter_MatchesANativeLabelByItsEnglishLanguageName()
    {
        var picker = new SubtitleTrackPicker { Options = [_english, _russian, _bitmap] };

        Assert.Equal([_russian], picker.Filter("russian"));
        Assert.Equal([_english, _russian, _bitmap], picker.Filter(string.Empty));
    }

    [AvaloniaFact]
    public void Commit_ADifferentTrack_SelectsItAndRaisesOptionPicked()
    {
        var picker = new SubtitleTrackPicker { Options = [_english, _russian], SelectedOption = _english };
        var raised = 0;
        picker.OptionPicked += (_, _) => raised++;

        picker.Commit(_russian);

        Assert.Same(_russian, picker.SelectedOption);
        Assert.Equal(1, raised);
    }

    [AvaloniaFact]
    public void Commit_TheCurrentTrack_DoesNotRaise()
    {
        var picker = new SubtitleTrackPicker { Options = [_english], SelectedOption = _english };
        var raised = 0;
        picker.OptionPicked += (_, _) => raised++;

        picker.Commit(_english);

        Assert.Equal(0, raised);
    }

    [AvaloniaFact]
    public void Commit_AnUnsupportedTrack_IsRefused()
    {
        var picker = new SubtitleTrackPicker { Options = [_english, _bitmap], SelectedOption = _english };
        var raised = 0;
        picker.OptionPicked += (_, _) => raised++;

        picker.Commit(_bitmap);

        Assert.Same(_english, picker.SelectedOption);
        Assert.Equal(0, raised);
    }

    [AvaloniaFact]
    public void SettingSelectedOptionFromCode_DoesNotRaise()
    {
        var picker = new SubtitleTrackPicker { Options = [_english, _russian] };
        var raised = 0;
        picker.OptionPicked += (_, _) => raised++;

        picker.SelectedOption = _russian;

        Assert.Equal(0, raised);
    }

    [AvaloniaFact]
    public void NoSelection_ShowsThePlaceholder()
    {
        var picker = new SubtitleTrackPicker { PlaceholderText = "Select track" };

        Assert.Equal("Select track", picker.SelectionText.Text);

        picker.SelectedOption = _english;

        Assert.Equal(_english.Label, picker.SelectionText.Text);
    }
}
