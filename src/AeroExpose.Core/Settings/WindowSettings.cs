namespace AeroExpose.Core.Settings;

public sealed class WindowSettings
{
    public bool ShowMinimized { get; set; } = true;
    public bool ShowAllMonitors { get; set; } = true;
    public bool ShowTitles { get; set; } = true;
    public bool ShowIcons { get; set; } = true;
    public bool IncludeUntitled { get; set; }
    public double Spacing { get; set; } = 1d;
    public double PreviewScale { get; set; } = 1d;

    internal void Normalize()
    {
        Spacing = Math.Clamp(Spacing, 0.5d, 1.75d);
        PreviewScale = Math.Clamp(PreviewScale, 0.75d, 1.25d);
    }
}
