# Manual verification matrix

Run a Release x64 build with debug mode off first, then repeat diagnostic cases with `DebugOverlayEnabled` set to `true` in `%LOCALAPPDATA%\AeroExpose\settings.json`.

## Core interaction

- Start the app; verify no main window or taskbar button remains.
- Press `Ctrl + Alt + Space`; verify the cursor monitor dims before previews settle.
- Press the shortcut again during opening, while open, and during closing; verify no stuck overlay or crash.
- Press Escape; verify the prior active app keeps focus after the overlay closes.
- Hover several previews rapidly; verify scaling settles without oscillation or overlap becoming distracting.
- Click a normal, maximized, and minimized window; verify return motion, restoration, and that the selected window remains in front after the overlay closes.
- Use all arrow directions, Enter, and Space; verify spatial selection and activation.
- Close an application while its preview is visible, then click the stale preview; verify AeroExpose stays open and disables that target.

## Layout populations

Verify 1, 2, 3, 5, 10, 15, and more than 20 windows, including portrait documents, ultrawide terminals, square utilities, and maximized windows. Check aspect ratio, gaps, centering, titles, and taskbar avoidance.

## DPI and displays

- Repeat at 100%, 125%, 150%, 175%, and 200% scaling.
- Test negative-coordinate monitors to the left and above the primary display.
- Test mixed DPI and mixed resolution displays.
- Move the pointer between monitors before invoking the shortcut.
- Change scale/resolution or disconnect the target monitor while open; verify the stale overview dismisses cleanly.
- Test taskbars on every edge and auto-hide mode.

## DWM and application classes

- Win32, WPF, UWP/packaged, Chromium, Electron, console/terminal, media, elevated, and minimized applications.
- A fullscreen borderless app and, where possible, an exclusive-fullscreen app.
- Windows that close or change placement during opening/closing.
- Protected or secure surfaces: verify they are skipped/blank rather than captured or crashing the app.

## Performance

- With diagnostics enabled, verify animation remains close to the display refresh rate.
- Confirm metadata enumeration occurs once per toggle and DWM properties update only during active animations.
- Check working-set stability across at least 100 open/close cycles; DWM registrations and hotkeys must not accumulate.

## Trackpad

- On a Precision Touchpad, map four-finger swipe up to `Ctrl + Alt + Space` and repeat rapid-toggle and focus cases.
- Verify the system gesture remains configurable and uninstalling/stopping AeroExpose leaves the touchpad functional.
