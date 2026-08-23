using System;

namespace OMP.Ui.Extensions;

internal static class TimeFormatExt
{
    extension(TimeSpan time)
    {
        public string Format() => time.TotalHours >= 1
            ? time.ToString(@"hh\:mm\:ss")
            : time.ToString(@"mm\:ss");
    }
}
