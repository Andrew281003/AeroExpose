# AeroExpose

AeroExpose is a Windows 10 x64 desktop utility that recreates the *overview* feel of Mission Control / Exposé: one global shortcut dims the current display and animates its real application windows into a balanced, live overview. It is deliberately not a Stage Manager clone.

> **Demo capture placeholder** — add `docs/media/aeroexpose-overview.gif` after the visual test matrix has been recorded on representative 100%, 150%, and 200% DPI systems.

## Current feature set

- Global `Ctrl + Alt + Space` toggle with no-repeat registration.
- Filtered top-level Win32 window discovery and cached application metadata.
- Live previews through `DwmRegisterThumbnail`, with SafeHandle ownership and graceful fallback cards.
- Scored, aspect-preserving, non-grid row packing for mixed portrait, landscape, and ultrawide windows.
- Frame-synchronized opening, closing, hover, and selected-window return animations.
- Cursor selection, arrow-key navigation, Enter/Space selection, and Escape dismissal.
- Restore and best-effort foreground activation with a taskbar-flash fallback.
- Per-Monitor-v2 DPI coordinates and a cursor-monitor Phase 1 monitor policy.
- Windows 10 backdrop blur where available, plus a reliable configurable dim fallback.
- Optional diagnostics for layout score, HWNDs, source/target bounds, DPI, monitor, DWM state, and composition FPS.
- Background-host lifetime with a system tray menu, lazy Settings UI, and clean explicit shutdown.
- Single-instance command forwarding through a per-user named pipe.
- Strongly typed, live-applied JSON settings with automatic migration from the original flat schema.

## Requirements

- Windows 10 x64, version 1703 or newer. Windows 10 22H2 is the primary development target.
- [.NET 9 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) for building from source. The published executable includes its own runtime.
- Visual Studio 2022 with the **.NET desktop development** workload, or the .NET CLI.
- Desktop Window Manager composition enabled for live thumbnails.

The solution contains no third-party NuGet dependency. The `global.json` prefers .NET 9 and permits a newer installed SDK to target .NET 9.

## Build and run

From a Developer PowerShell prompt:

```powershell
dotnet build AeroExpose.sln -c Release -p:Platform=x64
dotnet run --project src\AeroExpose.App\AeroExpose.App.csproj -c Release -p:Platform=x64
```

In Visual Studio, open `AeroExpose.sln`, select **x64**, make `AeroExpose.App` the startup project, and press **F5**. AeroExpose has no permanent main window; use the tray icon for Settings or press `Ctrl + Alt + Space`.

To create the standalone executable:

```powershell
dotnet publish src\AeroExpose.App\AeroExpose.App.csproj -c Release -o dist
```

The result is `dist\AeroExpose.exe`. It is self-contained, so the target computer does not need .NET installed. Put the executable in its permanent location before running it for the first time.

Startup registration is opt-in from **Settings → General** and uses the current user's Run key, so it requires no administrator access.

An already-running instance accepts `--settings`, `--toggle`, `--show`, `--hide`, and `--exit`. Launching the executable a second time without a command opens Settings in the existing process.

Run the dependency-free logic suite with:

```powershell
dotnet run --project tests\AeroExpose.Tests\AeroExpose.Tests.csproj -c Release
```

Set `AEROEXPOSE_SMOKE_TEST=1` before launch to exercise discovery, opening animation,
selection return, activation, cleanup, and automatic shutdown. The smoke path deliberately
selects a different eligible window and exits with a failure code unless that HWND becomes and
remains the foreground window for 1.5 seconds. Set `AEROEXPOSE_SMOKE_RESULT_PATH` to capture the
foreground-HWND transition trace; set `AEROEXPOSE_SMOKE_MINIMIZE_TARGET=1` to cover restoration.

## Controls

| Action | Input |
|---|---|
| Open or close overview | `Ctrl + Alt + Space` |
| Highlight a preview | Pointer hover or arrow keys |
| Select a window | Left click, Enter, or Space |
| Dismiss without changing the active app | Escape |

The controller also exposes `MissionControlController.Toggle()`, `Show()`, and `Hide()` as in-process integration points. An out-of-process gesture helper should send the configured global shortcut.

## Architecture

```text
src/
  AeroExpose.Core/
    Layout/             scored, UI-independent packing
    Models/             physical-pixel window/monitor/session models
    Services/           JSON serialization
    Settings/           configurable behavior
    Utilities/          DPI and coordinate math
    WindowManagement/   independently tested eligibility rules
  AeroExpose.App/
    App/                MissionControlController orchestration
    Animation/          composition-clock animation and easing
    Diagnostics/        opt-in FPS measurement
    Input/              message-window global hotkey
    Native/             centralized User32, DWM, and Shcore interop
    Rendering/          live thumbnails and desktop backdrop
    Services/           settings persistence
    UI/                 WPF overlay and preview chrome
    WindowManagement/   discovery, monitors, icons, activation
tests/
  AeroExpose.Tests/     layout, filtering, coordinates, DPI, settings
```

WPF was selected because the product is fundamentally HWND- and DWM-thumbnail-driven on Windows 10. Rendering decisions are kept out of the core and controller so a future WinUI 3 or DirectComposition surface can retain the window, layout, settings, and input layers.

### Native API boundaries

- `EnumWindows`, window styles, ownership, class, visibility, placement, and `DWMWA_CLOAKED` drive eligibility.
- `DWMWA_EXTENDED_FRAME_BOUNDS` supplies physical source geometry.
- `DwmRegisterThumbnail`, `DwmQueryThumbnailSourceSize`, and `DwmUpdateThumbnailProperties` supply live frames. Every registration is owned by `SafeDwmThumbnailHandle` and unregistered deterministically.
- `RegisterHotKey` runs on a message-only HWND and is always unregistered during shutdown.
- The manifest declares Per-Monitor-v2 awareness; the layout engine uses physical pixels, while only WPF chrome is converted to DIPs.
- Activation starts from a user action, restores minimized windows, briefly joins input queues, requests foreground/focus, then always detaches. Windows retains final authority over foreground transfer.

Microsoft API references: [DWM thumbnail overview](https://learn.microsoft.com/en-us/windows/win32/dwm/thumbnail-ovw), [DwmUpdateThumbnailProperties](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmupdatethumbnailproperties), [Per-Monitor-v2 awareness](https://learn.microsoft.com/en-us/windows/win32/hidpi/setting-the-default-dpi-awareness-for-a-process), and [SetForegroundWindow restrictions](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow).

## Settings

Settings are loaded from `%LOCALAPPDATA%\AeroExpose\settings.json`. Missing or invalid files fall back to normalized defaults. See [`docs/settings.example.json`](docs/settings.example.json).

Settings are organized into General, Appearance, Animations, Input, Windows, Advanced, and About pages. Closing Settings leaves the background host running. Animation settings include a live miniature-window preview and normal options apply without restarting AeroExpose.

## Precision Touchpad path

For Windows 10, the recommended integration is to let Windows recognize the four-finger swipe and map **Swipe up** to AeroExpose's `Ctrl + Alt + Space` shortcut under **Settings → Devices → Touchpad → Advanced gesture configuration**. This keeps OEM/Windows palm rejection and global gesture arbitration intact and requires no HID filter driver. See [`docs/TRACKPAD_INTEGRATION.md`](docs/TRACKPAD_INTEGRATION.md).

## Known limitations

- The default `CursorMonitor` mode lays out windows belonging to the monitor under the pointer. `AllWindowsOnCursorMonitor` is available; coordinated one-overlay-per-monitor presentation is reserved by `PerMonitor` but not yet implemented.
- DWM can return an accepted thumbnail relationship that is blank, stale, or unavailable for minimized, protected, elevated, secure-desktop, or unusual compositor surfaces. AeroExpose never captures secure content and shows a fallback card when registration itself fails.
- The public DWM thumbnail API has no rounded-clip geometry. Preview chrome is rounded; the live image remains rectangular.
- Windows can deny foreground activation even after a user click. AeroExpose then flashes the app's taskbar button rather than using persistent or invasive focus hacks.
- Exclusive fullscreen applications can remain above normal desktop composition or temporarily interrupt DWM previews.
- The Windows 10 accent backdrop entry point is not a documented public contract. It is isolated behind `DesktopBackdropService`; documented DWM blur and dim-only behavior remain available when it fails.
- No installer, tray menu, or settings UI is included yet. Auto-start is enabled for the current user when the published executable is launched.

## Verification

The automated suite covers layout scenarios from 1 through 21 windows, extreme aspect ratios, non-overlap, bounds, filter rules, DPI conversion at 100–200%, monitor-coordinate translation, and settings normalization. The application smoke path runs the real WPF/DWM lifecycle.

Native visual behavior still requires the manual matrix in [`docs/MANUAL_TEST_PLAN.md`](docs/MANUAL_TEST_PLAN.md), particularly across GPUs, protected apps, monitor hot-plug, and every requested DPI scale.
