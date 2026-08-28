using System.Text.Json.Serialization;

namespace AeroExpose.Core.Settings;

public sealed class MissionControlSettings
{
    public const uint VirtualKeySpace = 0x20;

    public GeneralSettings General { get; set; } = new();
    public AnimationSettings Animations { get; set; } = new();
    public AppearanceSettings Appearance { get; set; } = new();
    public InputSettings Input { get; set; } = new();
    public WindowSettings Windows { get; set; } = new();
    public AdvancedSettings Advanced { get; set; } = new();
    public SettingsWindowState SettingsWindow { get; set; } = new();

    // These adapters preserve the existing Mission Control API while JSON uses grouped settings.
    [JsonIgnore] public HotkeyModifiers ShortcutModifiers { get => Input.ShortcutModifiers; set => Input.ShortcutModifiers = value; }
    [JsonIgnore] public uint ShortcutVirtualKey { get => Input.ShortcutVirtualKey; set => Input.ShortcutVirtualKey = value; }
    [JsonIgnore] public int AnimationDurationMilliseconds { get => Animations.EffectiveOpenDurationMs; set => Animations.DurationMs = value; }
    [JsonIgnore] public int CloseAnimationDurationMilliseconds { get => Animations.EffectiveCloseDurationMs; set => Animations.CloseDurationMs = value; }
    [JsonIgnore] public int HoverAnimationDurationMilliseconds { get; set; } = 130;
    [JsonIgnore] public double BackgroundDimAmount { get => Appearance.DimStrength; set => Appearance.DimStrength = value; }
    [JsonIgnore] public bool BlurEnabled { get => Appearance.BackgroundEffect is BackgroundEffect.Blur or BackgroundEffect.BlurAndDim; set => Appearance.BackgroundEffect = value ? BackgroundEffect.BlurAndDim : BackgroundEffect.Dim; }
    [JsonIgnore] public bool ShowWindowTitles { get => Windows.ShowTitles && Appearance.ShowTitles; set { Windows.ShowTitles = value; Appearance.ShowTitles = value; } }
    [JsonIgnore] public double PreviewSpacing { get => Windows.Spacing * 28d; set => Windows.Spacing = value / 28d; }
    [JsonIgnore] public double HoverScale { get => Appearance.HoverScale; set => Appearance.HoverScale = value; }
    [JsonIgnore] public bool ShowMinimizedWindows { get => Windows.ShowMinimized; set => Windows.ShowMinimized = value; }
    [JsonIgnore] public MonitorMode MonitorMode { get => Windows.ShowAllMonitors ? MonitorMode.AllWindowsOnCursorMonitor : MonitorMode.CursorMonitor; set => Windows.ShowAllMonitors = value == MonitorMode.AllWindowsOnCursorMonitor; }
    [JsonIgnore] public bool DebugOverlayEnabled { get => Advanced.DebugMode; set => Advanced.DebugMode = value; }

    public void Normalize()
    {
        General ??= new GeneralSettings();
        Animations ??= new AnimationSettings();
        Appearance ??= new AppearanceSettings();
        Input ??= new InputSettings();
        Windows ??= new WindowSettings();
        Advanced ??= new AdvancedSettings();
        SettingsWindow ??= new SettingsWindowState();
        Animations.Normalize();
        Appearance.Normalize();
        Input.Normalize();
        Windows.Normalize();
        SettingsWindow.Normalize();
        HoverAnimationDurationMilliseconds = Math.Clamp(HoverAnimationDurationMilliseconds, 60, 400);
    }
}
