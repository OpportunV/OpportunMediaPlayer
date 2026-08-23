using System.Globalization;
using System.Resources;

namespace OMP.Ui.Localization;

// ReSharper disable InconsistentNaming - property names mirror Strings.resx keys 1:1.
internal static class Strings
{
    public static string MainWindow_FileMenu => Get("MainWindow_FileMenu");

    public static string MainWindow_OpenMenuItem => Get("MainWindow_OpenMenuItem");

    public static string MainWindow_OpenUrlMenuItem => Get("MainWindow_OpenUrlMenuItem");

    public static string MainWindow_OptionsMenuItem => Get("MainWindow_OptionsMenuItem");

    public static string MainWindow_ExitMenuItem => Get("MainWindow_ExitMenuItem");

    public static string MainWindow_HelpMenu => Get("MainWindow_HelpMenu");

    public static string MainWindow_HotkeysMenuItem => Get("MainWindow_HotkeysMenuItem");

    public static string MainWindow_AboutMenuItem => Get("MainWindow_AboutMenuItem");

    public static string MainWindow_SpeedButtonTooltip => Get("MainWindow_SpeedButtonTooltip");

    public static string MainWindow_PlaybackSpeedHeader => Get("MainWindow_PlaybackSpeedHeader");

    public static string MainWindow_SubtitlesButtonTooltip => Get("MainWindow_SubtitlesButtonTooltip");

    public static string MainWindow_OutputVolumesButtonTooltip => Get("MainWindow_OutputVolumesButtonTooltip");

    public static string MainWindow_OutputVolumesHeader => Get("MainWindow_OutputVolumesHeader");

    public static string MainWindow_OpenFileDialogTitle => Get("MainWindow_OpenFileDialogTitle");

    public static string MainWindow_OpenFileTypeFilterName => Get("MainWindow_OpenFileTypeFilterName");

    public static string MainWindow_NoVideoLabel => Get("MainWindow_NoVideoLabel");

    public static string MainWindow_OpeningLabel => Get("MainWindow_OpeningLabel");

    public static string MainWindow_EmptyStateLabel => Get("MainWindow_EmptyStateLabel");

    public static string MainWindow_EmptyStateLabelNoDragDrop => Get("MainWindow_EmptyStateLabelNoDragDrop");

    public static string Options_Title => Get("Options_Title");

    public static string Options_GeneralTab => Get("Options_GeneralTab");

    public static string Options_AudioRoutingTab => Get("Options_AudioRoutingTab");

    public static string Options_SubtitlesTab => Get("Options_SubtitlesTab");

    public static string Options_SubtitleZonesTab => Get("Options_SubtitleZonesTab");

    public static string Options_ThemeLabel => Get("Options_ThemeLabel");

    public static string Options_ThemeHint => Get("Options_ThemeHint");

    public static string Options_MuteOutputTooltip => Get("Options_MuteOutputTooltip");

    public static string Options_DelayMsTooltip => Get("Options_DelayMsTooltip");

    public static string Options_StreamHeader => Get("Options_StreamHeader");

    public static string Options_OutputHeader => Get("Options_OutputHeader");

    public static string Options_VolumeHeader => Get("Options_VolumeHeader");

    public static string Options_DelayMsHeader => Get("Options_DelayMsHeader");

    public static string Options_SelectOutputPlaceholder => Get("Options_SelectOutputPlaceholder");

    public static string Options_SelectStreamPlaceholder => Get("Options_SelectStreamPlaceholder");

    public static string Options_SubtitlesHelpText => Get("Options_SubtitlesHelpText");

    public static string Options_TrackHeader => Get("Options_TrackHeader");

    public static string Options_ZoneHeader => Get("Options_ZoneHeader");

    public static string Options_SelectTrackPlaceholder => Get("Options_SelectTrackPlaceholder");

    public static string Options_SelectZonePlaceholder => Get("Options_SelectZonePlaceholder");

    public static string Options_LoadSubtitleFileButton => Get("Options_LoadSubtitleFileButton");

    public static string Options_LoadSubtitleFileTitle => Get("Options_LoadSubtitleFileTitle");

    public static string Options_SubtitleFileTypeFilterName => Get("Options_SubtitleFileTypeFilterName");

    public static string Options_SubtitleRouteError => Get("Options_SubtitleRouteError");

    public static string Options_ZonesHelpText => Get("Options_ZonesHelpText");

    public static string Options_EditZoneButton => Get("Options_EditZoneButton");

    public static string Options_ResetZoneButton => Get("Options_ResetZoneButton");

    public static string Options_ResetZoneTooltip => Get("Options_ResetZoneTooltip");

    public static string Options_AddZoneButton => Get("Options_AddZoneButton");

    public static string Options_LanguageLabel => Get("Options_LanguageLabel");

    public static string Options_LanguageHint => Get("Options_LanguageHint");

    public static string Options_LanguageRestartButton => Get("Options_LanguageRestartButton");

    public static string Options_YtDlpPathLabel => Get("Options_YtDlpPathLabel");

    public static string Options_YtDlpPathHint => Get("Options_YtDlpPathHint");

    public static string Options_YtDlpPathWatermarkShort => Get("Options_YtDlpPathWatermarkShort");

    public static string Options_YtDlpPathResetButton => Get("Options_YtDlpPathResetButton");

    public static string Options_YtDlpPathResetTooltip => Get("Options_YtDlpPathResetTooltip");

    public static string Options_YtDlpPathBrowseButton => Get("Options_YtDlpPathBrowseButton");

    public static string Options_YtDlpPathBrowseTitle => Get("Options_YtDlpPathBrowseTitle");

    public static string Options_YtDlpPathFileTypeFilterName => Get("Options_YtDlpPathFileTypeFilterName");

    public static string ThemeMode_Light => Get("ThemeMode_Light");

    public static string ThemeMode_Dark => Get("ThemeMode_Dark");

    public static string Common_SystemDefault => Get("Common_SystemDefault");

    public static string Hotkeys_Title => Get("Hotkeys_Title");

    public static string Hotkeys_GroupPlayback => Get("Hotkeys_GroupPlayback");

    public static string Hotkeys_GroupAudio => Get("Hotkeys_GroupAudio");

    public static string Hotkeys_GroupView => Get("Hotkeys_GroupView");

    public static string Hotkeys_PlayPause => Get("Hotkeys_PlayPause");

    public static string Hotkeys_StepBack => Get("Hotkeys_StepBack");

    public static string Hotkeys_StepForward => Get("Hotkeys_StepForward");

    public static string Hotkeys_StepBackForward => Get("Hotkeys_StepBackForward");

    public static string Hotkeys_ToggleFullscreen => Get("Hotkeys_ToggleFullscreen");

    public static string Hotkeys_ToggleSubtitles => Get("Hotkeys_ToggleSubtitles");

    public static string Hotkeys_ToggleMute => Get("Hotkeys_ToggleMute");

    public static string Hotkeys_ExitFullscreen => Get("Hotkeys_ExitFullscreen");

    public static string Hotkeys_DecreaseSpeed => Get("Hotkeys_DecreaseSpeed");

    public static string Hotkeys_IncreaseSpeed => Get("Hotkeys_IncreaseSpeed");

    public static string Hotkeys_SpeedDownUp => Get("Hotkeys_SpeedDownUp");

    public static string Hotkeys_IncreaseVolume => Get("Hotkeys_IncreaseVolume");

    public static string Hotkeys_DecreaseVolume => Get("Hotkeys_DecreaseVolume");

    public static string Hotkeys_VolumeDownUp => Get("Hotkeys_VolumeDownUp");

    public static string About_Title => Get("About_Title");

    public static string About_LicenseText => Get("About_LicenseText");

    public static string About_GitHubButton => Get("About_GitHubButton");

    public static string About_VersionFormat => Get("About_VersionFormat");

    public static string Common_Close => Get("Common_Close");

    public static string AudioWarning_Title => Get("AudioWarning_Title");

    public static string AudioWarning_Heading => Get("AudioWarning_Heading");

    public static string AudioWarning_TechnicalDetailLabel => Get("AudioWarning_TechnicalDetailLabel");

    public static string AudioWarning_LinuxGuidance => Get("AudioWarning_LinuxGuidance");

    public static string AudioWarning_DefaultGuidance => Get("AudioWarning_DefaultGuidance");

    public static string OpenFileError_Title => Get("OpenFileError_Title");

    public static string OpenFileError_Heading => Get("OpenFileError_Heading");

    public static string OpenFileError_FFmpegMacHeading => Get("OpenFileError_FFmpegMacHeading");

    public static string OpenFileError_TechnicalDetailLabel => Get("OpenFileError_TechnicalDetailLabel");

    public static string OpenFileError_SubtitleHeading => Get("OpenFileError_SubtitleHeading");

    public static string OpenFileError_YtDlpNotFoundWindowsHeading => Get("OpenFileError_YtDlpNotFoundWindowsHeading");

    public static string OpenFileError_YtDlpNotFoundMacHeading => Get("OpenFileError_YtDlpNotFoundMacHeading");

    public static string OpenFileError_YtDlpNotFoundLinuxHeading => Get("OpenFileError_YtDlpNotFoundLinuxHeading");

    public static string OpenUrl_Title => Get("OpenUrl_Title");

    public static string OpenUrl_Label => Get("OpenUrl_Label");

    public static string OpenUrl_Watermark => Get("OpenUrl_Watermark");

    public static string OpenUrl_ResolvingLabel => Get("OpenUrl_ResolvingLabel");

    public static string OpenUrl_InvalidUrlError => Get("OpenUrl_InvalidUrlError");

    public static string OpenUrl_OkButton => Get("OpenUrl_OkButton");

    public static string OpenUrl_CancelButton => Get("OpenUrl_CancelButton");

    public static string OpenUrl_TimeoutError => Get("OpenUrl_TimeoutError");

    public static string OpenUrl_GenericResolveError => Get("OpenUrl_GenericResolveError");

    public static string OpenUrl_NoPlayableFormatError => Get("OpenUrl_NoPlayableFormatError");

    public static string SubtitleZoneEditor_NewTitle => Get("SubtitleZoneEditor_NewTitle");

    public static string SubtitleZoneEditor_EditTitleFormat => Get("SubtitleZoneEditor_EditTitleFormat");

    public static string SubtitleZoneEditor_ScreenLabel => Get("SubtitleZoneEditor_ScreenLabel");

    public static string SubtitleZoneEditor_VideoLabel => Get("SubtitleZoneEditor_VideoLabel");

    public static string SubtitleZoneEditor_DragHint => Get("SubtitleZoneEditor_DragHint");

    public static string SubtitleZoneEditor_NameWatermark => Get("SubtitleZoneEditor_NameWatermark");

    public static string SubtitleZoneEditor_NameHint => Get("SubtitleZoneEditor_NameHint");

    public static string SubtitleZoneEditor_FontLabel => Get("SubtitleZoneEditor_FontLabel");

    public static string SubtitleZoneEditor_SizeLabel => Get("SubtitleZoneEditor_SizeLabel");

    public static string SubtitleZoneEditor_TextColorLabel => Get("SubtitleZoneEditor_TextColorLabel");

    public static string SubtitleZoneEditor_BgColorLabel => Get("SubtitleZoneEditor_BgColorLabel");

    public static string SubtitleZoneEditor_BgOpacityLabel => Get("SubtitleZoneEditor_BgOpacityLabel");

    public static string SubtitleZoneEditor_AlignHLabel => Get("SubtitleZoneEditor_AlignHLabel");

    public static string SubtitleZoneEditor_AlignVLabel => Get("SubtitleZoneEditor_AlignVLabel");

    public static string SubtitleZoneEditor_CancelButton => Get("SubtitleZoneEditor_CancelButton");

    public static string SubtitleZoneEditor_SaveButton => Get("SubtitleZoneEditor_SaveButton");

    public static string SubtitleZoneEditor_SamplePrefix => Get("SubtitleZoneEditor_SamplePrefix");

    public static string SubtitleZoneEditor_SampleWith => Get("SubtitleZoneEditor_SampleWith");

    public static string SubtitleZoneEditor_SampleBold => Get("SubtitleZoneEditor_SampleBold");

    public static string SubtitleZoneEditor_SampleAnd => Get("SubtitleZoneEditor_SampleAnd");

    public static string SubtitleZoneEditor_SampleItalic => Get("SubtitleZoneEditor_SampleItalic");

    public static string SubtitleZoneEditor_SampleStyles => Get("SubtitleZoneEditor_SampleStyles");

    public static string SubtitleZoneEditor_DefaultZoneName => Get("SubtitleZoneEditor_DefaultZoneName");

    public static string Common_AlignLeft => Get("Common_AlignLeft");

    public static string Common_AlignCenter => Get("Common_AlignCenter");

    public static string Common_AlignRight => Get("Common_AlignRight");

    public static string Common_AlignTop => Get("Common_AlignTop");

    public static string Common_AlignBottom => Get("Common_AlignBottom");

    private static readonly ResourceManager _resourceManager = new(typeof(Strings));

    private static string Get(string key) => _resourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
}
// ReSharper restore InconsistentNaming
