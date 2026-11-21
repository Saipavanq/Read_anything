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

        for (var index = 0; index < tray.MenuItems.Count; index++)
        {
            var item = new ToolStripMenuItem(tray.MenuItems[index]);
            item.Click += (_, _) => Invoke(index);
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

    private void Invoke(int index)
    {
        switch (index)
        {
            case 0:
                _tray.ShowWindow();
                break;
            case 1:
                _tray.ReadSelection();
                break;
            case 2:
                _tray.Stop();
                break;
            case 3:
                _tray.OpenSettings();
                break;
            case 4:
                _tray.Quit();
                break;
        }
    }
}
