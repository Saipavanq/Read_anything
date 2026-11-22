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
            Icon = LoadIcon(),
            Text = "Read Aloud",
            ContextMenuStrip = _menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => _tray.ShowWindow();
    }

    private static Icon LoadIcon()
    {
        var uri = new Uri("pack://application:,,,/Assets/app-icon.ico", UriKind.Absolute);
        using var stream = System.Windows.Application.GetResourceStream(uri)?.Stream
            ?? throw new InvalidOperationException("Read Aloud app icon resource is missing.");
        return new Icon(stream);
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
