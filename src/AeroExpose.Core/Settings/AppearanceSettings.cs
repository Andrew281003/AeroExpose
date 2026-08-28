namespace AeroExpose.Core.Settings;

public sealed class AppearanceSettings
{
    public ThemeMode Theme { get; set; } = ThemeMode.System;
    public BackgroundEffect BackgroundEffect { get; set; } = BackgroundEffect.BlurAndDim;
    public double DimStrength { get; set; } = 0.35d;
    public PreviewCorners PreviewCorners { get; set; } = PreviewCorners.System;
    public bool PreviewShadow { get; set; } = true;
    public bool ShowTitles { get; set; } = true;
    public bool ShowIcons { get; set; } = true;
    public HoverEffect HoverEffect { get; set; } = HoverEffect.Scale;
    public double HoverScale { get; set; } = 1.035d;

    internal void Normalize()
    {
        DimStrength = Math.Clamp(DimStrength, 0d, 0.8d);
        HoverScale = Math.Clamp(HoverScale, 1d, 1.12d);
    }
}

public enum ThemeMode { System, Light, Dark }
public enum BackgroundEffect { Blur, Dim, BlurAndDim, None }
public enum PreviewCorners { System, Rounded, Square }
public enum HoverEffect { None, Highlight, Scale, Glow }
