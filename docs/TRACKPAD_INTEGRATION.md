# Trackpad integration

## Recommended Windows 10 route

AeroExpose deliberately treats a gesture as an input trigger, not as part of the renderer. On a laptop with a Windows Precision Touchpad:

1. Start AeroExpose and confirm `Ctrl + Alt + Space` opens the overview.
2. Open **Settings → Devices → Touchpad**.
3. Open **Advanced gesture configuration**.
4. Under **Configure four-finger gestures**, set **Swipe up** to **Custom shortcut**.
5. Record `Ctrl + Alt + Space` and save it.

Microsoft documents that users can customize three- and four-finger actions in Touchpad settings. Windows then owns contact tracking, palm rejection, thresholds, and arbitration, while AeroExpose keeps one reliable `RegisterHotKey` entry point. This is the lowest-risk and most native Windows 10 integration.

Reference: [Microsoft Support — Touch gestures for Windows](https://support.microsoft.com/en-us/windows/hardware/input-devices/touch-gestures-for-windows).

## Why AeroExpose does not parse raw HID in Phase 1

- Precision Touchpad HID reports are part of the system input stack; consuming or filtering them globally can conflict with the shell and OEM behavior.
- A filter driver adds administration, signing, update, and device-compatibility burdens.
- `WM_GESTURE` and ordinary mouse-wheel input do not reliably identify a four-finger global swipe.
- OEM non-Precision touchpads expose different data and often require their own gesture software.

If the device does not offer Windows' custom shortcut setting, the preferred fallback is its OEM gesture utility or a user-selected remapper that emits AeroExpose's shortcut. No remapper is required by the application itself.

## Newer Windows API note

Microsoft's 2026 `TouchpadGesturesController` and `PhysicalGestureRecognizer` APIs expose three-or-more-finger gestures at higher fidelity, but Microsoft lists Windows 11 as the minimum client. More importantly, global gestures are offered only to a registered **foreground process**; background processes are ignored. That makes the API useful for manipulating an already visible AeroExpose session, but not a Windows 10-compatible replacement for the global shortcut that opens it.

References: [Precision Touchpad Input](https://learn.microsoft.com/en-us/windows/win32/input-precisiontouchpad/precision-touchpad-portal), [TouchpadGesturesController](https://learn.microsoft.com/en-us/windows/win32/input-precisiontouchpad/touchpadgesturescontroller).

## Future adapter contract

An in-process adapter calls `MissionControlController.Show()` when its upward-swipe recognizer commits. An out-of-process adapter sends the configured global shortcut; a future named-pipe trigger can be added without changing the controller, layout engine, overlay, or DWM renderer.
