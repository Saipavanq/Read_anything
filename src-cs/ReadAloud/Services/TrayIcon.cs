using System;
using System.Drawing;
using System.Windows.Forms;

namespace ReadAloud.Services;

internal sealed class TrayIcon : IDisposable
{
    private readonly TrayService _tray;
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu;
    private bool _disposed;

    public TrayIcon(TrayService tray)
    {
        _tray = tray ?? throw new ArgumentNullException(nameof(tray));
        _menu = new ContextMenuStrip();

        foreach (var (label, invoke) in tray.MenuActions)
        {
            var item = new ToolStripMenuItem(label);
            item.Click += (_, _) => invoke();
            _menu.Items.Add(item);
        }

        _notifyIcon = new NotifyIcon
        {
            Icon = (Icon)SystemIcons.Application.Clone(),
            Text = "Read Aloud",
            ContextMenuStrip = _menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => _tray.ShowWindow();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
        _disposed = true;
    }
}
