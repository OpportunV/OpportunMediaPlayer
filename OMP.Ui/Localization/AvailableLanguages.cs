using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace OMP.Ui.Localization;

internal static class AvailableLanguages
{
    public static IReadOnlyList<CultureInfo> Cultures { get; } = Discover();

    private static IReadOnlyList<CultureInfo> Discover()
    {
        var cultures = new List<CultureInfo> { CultureInfo.GetCultureInfo("en") };
        var assemblyName = typeof(Strings).Assembly.GetName().Name;
        var baseDirectory = AppContext.BaseDirectory;

        if (assemblyName is null || !Directory.Exists(baseDirectory))
        {
            return cultures;
        }

        foreach (var directory in Directory.EnumerateDirectories(baseDirectory))
        {
            if (!File.Exists(Path.Combine(directory, $"{assemblyName}.resources.dll")))
            {
                continue;
            }

            try
            {
                cultures.Add(CultureInfo.GetCultureInfo(Path.GetFileName(directory)));
            }
            catch (CultureNotFoundException)
            {
            }
        }

        return cultures.AsReadOnly();
    }
}
