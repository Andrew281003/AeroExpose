namespace AeroExpose.Core.Settings;

public sealed class SettingsWindowState
{
    public double? Left { get; set; }
    public double? Top { get; set; }
    public double Width { get; set; } = 940;
    public double Height { get; set; } = 680;
    public string SelectedPage { get; set; } = "General";

    internal void Normalize()
    {
        Width = Math.Clamp(Width, 760, 1600);
        Height = Math.Clamp(Height, 540, 1200);
    }
}
