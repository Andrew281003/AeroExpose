namespace AeroExpose.Core.Settings;

public sealed class InputSettings
{
    public HotkeyModifiers ShortcutModifiers { get; set; } = HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.NoRepeat;
    public uint ShortcutVirtualKey { get; set; } = MissionControlSettings.VirtualKeySpace;
    public GestureAction FourFingerSwipeUp { get; set; } = GestureAction.MissionControl;
    public EscapeBehavior EscapeBehavior { get; set; } = EscapeBehavior.CloseMissionControl;

    internal void Normalize() => ShortcutVirtualKey = ShortcutVirtualKey == 0 ? MissionControlSettings.VirtualKeySpace : ShortcutVirtualKey;
}

public enum GestureAction { MissionControl, Disabled }
public enum EscapeBehavior { CloseMissionControl, ReturnToPreviouslyActiveWindow }
