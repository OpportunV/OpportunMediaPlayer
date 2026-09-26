using OMP.Lib.Subtitle;

namespace OMP.Ui.Extensions;

public static class SubtitleStreamExt
{
    extension(SubtitleStream stream)
    {
        public string Describe()
        {
            var description = stream.Codec == UnknownCodec
                ? $"{stream.Title} [{stream.Language}]"
                : $"{stream.Title} [{stream.Language}] ({stream.Codec})";

            return stream.IsTextBased ? description : $"{description} - unsupported";
        }
    }

    /// <summary>
    /// Mirrors OMP.Lib's internal placeholder for an absent tag - what every not-yet-opened web
    /// caption reports as its codec. Showing "(Unknown)" on ~160 caption entries is only noise.
    /// </summary>
    private const string UnknownCodec = "Unknown";
}
