using System;
using System.Collections.Generic;
using Avalonia.Layout;

namespace OMP.Ui.Settings;

public sealed class SubtitleZone
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "New Zone";

    public bool IsBuiltIn { get; set; }

    public bool IsDeletable => !IsBuiltIn;

    public double X { get; set; } = 0.1;

    public double Y { get; set; } = 0.75;

    public double Width { get; set; } = 0.8;

    public double Height { get; set; } = 0.15;

    public string FontFamily { get; set; } = "Segoe UI";

    public double FontSizeRatio { get; set; } = 0.045;

    public string FontColor { get; set; } = "#FFFFFF";

    public string BackgroundColor { get; set; } = "#000000";

    public double BackgroundOpacity { get; set; } = 0.6;

    public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.Center;

    public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Bottom;

    public const string BuiltInTopId = "built-in-top";
    public const string BuiltInBottomId = "built-in-bottom";

    public static List<SubtitleZone> CreateBuiltIns()
    {
        return
        [
            new SubtitleZone
            {
                Id = BuiltInTopId,
                Name = "Top",
                IsBuiltIn = true,
                X = 0.1,
                Y = 0.05,
                Width = 0.8,
                Height = 0.15,
                VerticalAlignment = VerticalAlignment.Top
            },
            new SubtitleZone
            {
                Id = BuiltInBottomId,
                Name = "Bottom",
                IsBuiltIn = true,
                X = 0.1,
                Y = 0.80,
                Width = 0.8,
                Height = 0.15,
                VerticalAlignment = VerticalAlignment.Bottom
            }
        ];
    }

    public SubtitleZone Clone()
    {
        return (SubtitleZone)MemberwiseClone();
    }
}
