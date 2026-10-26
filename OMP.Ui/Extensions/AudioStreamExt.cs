using System;
using OMP.Lib.Audio;

namespace OMP.Ui.Extensions;

public static class AudioStreamExt
{
    extension(AudioStream stream)
    {
        /// <summary>
        /// "[eng] Commentary (aac)", leaving out whatever the file doesn't say. An untitled track
        /// falls back to "Track N", where N is its 1-based position in the session's catalog.
        /// </summary>
        public string Describe()
        {
            var name = IsKnown(stream.Title) ? stream.Title : $"Track {stream.Id + 1}";
            var language = IsKnown(stream.Language) && !stream.Language.Equals(UndeterminedLanguage, StringComparison.OrdinalIgnoreCase)
                ? $"[{stream.Language}] "
                : string.Empty;
            var codec = IsKnown(stream.Codec) ? $" ({stream.Codec})" : string.Empty;

            return $"{language}{name}{codec}";
        }
    }

    /// <summary>
    /// Mirrors OMP.Lib's internal placeholder for an absent tag, same as <see cref="SubtitleStreamExt"/>.
    /// </summary>
    private const string Unknown = "Unknown";

    /// <summary>ISO 639-2's "undetermined" - what every MP4 track without a language reports.</summary>
    private const string UndeterminedLanguage = "und";

    private static bool IsKnown(string value) => value != Unknown;
}
