using System.Collections.Generic;
using OMP.Lib.Session;

namespace OMP.Ui.Helpers;

internal sealed record YtDlpMediaSelection(
    string Url,
    string? Title,
    IReadOnlyList<AudioSidecarSource> AudioSidecars,
    IReadOnlyDictionary<string, string>? Headers = null);
