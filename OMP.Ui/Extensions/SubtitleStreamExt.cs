using OMP.Lib.Subtitle;

namespace OMP.Ui.Extensions;

public static class SubtitleStreamExt
{
    extension(SubtitleStream stream)
    {
        public string Describe() => stream.IsTextBased
            ? $"{stream.Title} [{stream.Language}] ({stream.Codec})"
            : $"{stream.Title} [{stream.Language}] ({stream.Codec}) - unsupported";
    }
}
