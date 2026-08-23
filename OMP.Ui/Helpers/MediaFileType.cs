using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OMP.Ui.Helpers;

internal static class MediaFileType
{
    public static bool IsSupportedMediaFile(string path, IEnumerable<string> patterns)
    {
        var extension = Path.GetExtension(path);

        return !string.IsNullOrEmpty(extension) && patterns.Any(pattern =>
            pattern.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
    }
}
