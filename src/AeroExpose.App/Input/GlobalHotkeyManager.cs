using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using AeroExpose.Core.Settings;
using AeroExpose.Native;

namespace AeroExpose.Input;

internal sealed class GlobalHotkeyManager : IOverviewTrigger
{
    private const int HotkeyId = 0xAE01;
    private readonly HwndSource _messageWindow;
    private bool _registered;
    private bool _disposed;

    public GlobalHotkeyManager()
    {
        var parameters = new HwndSourceParameters("AeroExpose.GlobalHotkey")
        {
            ParentWindow = NativeMethods.HwndMessage,
            WindowStyle = 0,
        };
        _messageWindow = new HwndSource(parameters);
        _messageWindow.AddHook(WindowProcedure);
    }

    public event EventHandler? Triggered;

    public void Register(MissionControlSettings settings) =>
        Register(settings.ShortcutModifiers, settings.ShortcutVirtualKey);

    public void Register(HotkeyModifiers modifiers, uint virtualKey)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Unregister();

        if (!NativeMethods.RegisterHotKey(
                _messageWindow.Handle,
                HotkeyId,
                (uint)modifiers,
                virtualKey))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                "AeroExpose could not register that shortcut. Another application may already own it.");
        }

        _registered = true;
    }

    public void Unregister()
    {
        if (_registered)
        {
            NativeMethods.UnregisterHotKey(_messageWindow.Handle, HotkeyId);
            _registered = false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Unregister();
        _messageWindow.RemoveHook(WindowProcedure);
        _messageWindow.Dispose();
        _disposed = true;
    }

    private nint WindowProcedure(nint window, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == NativeMethods.WindowMessageHotkey && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            Triggered?.Invoke(this, EventArgs.Empty);
        }

        return nint.Zero;
    }
}
