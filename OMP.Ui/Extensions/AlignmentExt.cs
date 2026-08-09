using Avalonia.Layout;
using OMP.Ui.Localization;

namespace OMP.Ui.Extensions;

internal static class AlignmentExt
{
    extension(HorizontalAlignment value)
    {
        public string ToDisplayLabel() => value switch
        {
            HorizontalAlignment.Left => Strings.Common_AlignLeft,
            HorizontalAlignment.Right => Strings.Common_AlignRight,
            _ => Strings.Common_AlignCenter,
        };
    }

    extension(VerticalAlignment value)
    {
        public string ToDisplayLabel() => value switch
        {
            VerticalAlignment.Top => Strings.Common_AlignTop,
            VerticalAlignment.Bottom => Strings.Common_AlignBottom,
            _ => Strings.Common_AlignCenter,
        };
    }
}
