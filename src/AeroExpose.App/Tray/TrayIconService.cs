using System.Drawing;
using System.Windows.Forms;

namespace AeroExpose.Tray;

internal sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _enabledItem;
    private readonly ToolStripMenuItem _startupItem;
    private bool _disposed;

    public TrayIconService()
    {
        var menu = new ContextMenuStrip { ShowImageMargin = false };
        menu.Items.Add(new ToolStripMenuItem("AeroExpose") { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Open Mission Control", null, (_, _) => ToggleRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Settings", null, (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(new ToolStripSeparator());
        _enabledItem = new ToolStripMenuItem("Enable AeroExpose", null, (_, _) => EnabledToggleRequested?.Invoke(this, EventArgs.Empty))
        {
            CheckOnClick = false,
        };
        _startupItem = new ToolStripMenuItem("Start with Windows", null, (_, _) => StartupToggleRequested?.Invoke(this, EventArgs.Empty))
        {
            CheckOnClick = false,
        };
        menu.Items.Add(_enabledItem);
        menu.Items.Add(_startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit AeroExpose", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        var executable = Environment.ProcessPath;
        _notifyIcon = new NotifyIcon
        {
            Text = "AeroExpose",
            ContextMenuStrip = menu,
            Icon = !string.IsNullOrWhiteSpace(executable) ? Icon.ExtractAssociatedIcon(executable) : SystemIcons.Application,
        };
        _notifyIcon.MouseClick += OnMouseClick;
        _notifyIcon.MouseDoubleClick += OnMouseDoubleClick;
    }

    public event EventHandler? ToggleRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? EnabledToggleRequested;
    public event EventHandler? StartupToggleRequested;
    public event EventHandler? ExitRequested;

    public void Update(bool visible, bool enabled, bool startWithWindows)
    {
        _notifyIcon.Visible = visible;
        _enabledItem.Checked = enabled;
        _startupItem.Checked = startWithWindows;
    }

    public void ShowWarning(string message)
    {
        _notifyIcon.BalloonTipTitle = "AeroExpose";
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.BalloonTipIcon = ToolTipIcon.Warning;
        _notifyIcon.ShowBalloonTip(5000);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.MouseClick -= OnMouseClick;
        _notifyIcon.MouseDoubleClick -= OnMouseDoubleClick;
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.Icon?.Dispose();
        _notifyIcon.Dispose();
    }

    private void OnMouseClick(object? sender, MouseEventArgs eventArgs)
    {
        if (eventArgs.Button == MouseButtons.Left)
        {
            SettingsRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnMouseDoubleClick(object? sender, MouseEventArgs eventArgs)
    {
        if (eventArgs.Button == MouseButtons.Left)
        {
            SettingsRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
