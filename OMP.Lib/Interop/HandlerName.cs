namespace OMP.Lib.Interop;

/// <summary>
/// MP4/MOV have no per-track <c>title</c> tag: a track's name is stored in its <c>hdlr</c> box,
/// which FFmpeg exposes as <c>handler_name</c>. Most muxers fill that box with their own fixed
/// default instead of a real name, so only a value that doesn't look like one of those counts.
/// </summary>
internal static class HandlerName
{
    /// <summary>
    /// Muxer defaults that don't follow the "... Handler" shape: Apple's "Core Media Audio" and
    /// Google's "ISO Media file produced by Google Inc." (every YouTube-sourced MP4).
    /// </summary>
    private static readonly string[] _genericPrefixes = ["Core Media"];

    private static readonly string[] _genericFragments = ["produced by"];

    private const string GenericSuffix = "Handler";

    /// <summary>
    /// True when <paramref name="handlerName"/> is a real track name rather than a muxer default
    /// such as FFmpeg's "SoundHandler", GPAC's "GPAC ISO Audio Handler" or Apple's "Core Media Audio".
    /// </summary>
    public static bool IsTrackName(string? handlerName)
    {
        var name = handlerName?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        return !name.EndsWith(GenericSuffix, StringComparison.OrdinalIgnoreCase) &&
            !_genericPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) &&
            !_genericFragments.Any(fragment => name.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }
}
