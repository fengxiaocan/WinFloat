using System.Drawing;
using System.IO;
using Forms = System.Windows.Forms;
using WinFloat.Models;
using WinFloat.Views;

namespace WinFloat.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly MainWindow _window;
    private readonly StartupService _startupService;
    private readonly SettingsService _settingsService;
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Stream? _iconStream;
    private readonly Icon _applicationIcon;
    private readonly Forms.ToolStripMenuItem _showMenuItem;
    private readonly Forms.ToolStripMenuItem _lockMenuItem;
    private readonly Forms.ToolStripMenuItem _clickThroughMenuItem;
    private readonly Forms.ToolStripMenuItem _topmostMenuItem;
    private readonly Forms.ToolStripMenuItem _startupMenuItem;
    private bool _disposed;

    public TrayIconService(
        MainWindow window,
        StartupService startupService,
        SettingsService settingsService,
        Action exitAction)
    {
        _window = window;
        _startupService = startupService;
        _settingsService = settingsService;

        _showMenuItem = new Forms.ToolStripMenuItem();
        _lockMenuItem = new Forms.ToolStripMenuItem("锁定位置");
        _clickThroughMenuItem = new Forms.ToolStripMenuItem("鼠标穿透");
        _topmostMenuItem = new Forms.ToolStripMenuItem("始终置顶");
        _startupMenuItem = new Forms.ToolStripMenuItem("开机启动");

        _showMenuItem.Click += (_, _) => _window.ToggleVisibility();
        _lockMenuItem.Click += (_, _) => _window.SetLockWindow(!_window.Settings.LockWindow);
        _clickThroughMenuItem.Click += (_, _) => _window.SetClickThrough(!_window.Settings.ClickThrough);
        _topmostMenuItem.Click += (_, _) => _window.SetAlwaysOnTop(!_window.Settings.AlwaysOnTop);
        _startupMenuItem.Click += (_, _) => ToggleStartup();

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(_showMenuItem);
        menu.Items.Add(_lockMenuItem);
        menu.Items.Add(_clickThroughMenuItem);
        menu.Items.Add(_topmostMenuItem);
        menu.Items.Add(new Forms.ToolStripSeparator());

        var settingsItem = new Forms.ToolStripMenuItem("设置");
        settingsItem.Click += (_, _) => _window.OpenSettings();
        menu.Items.Add(settingsItem);

        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_startupMenuItem);

        var exitItem = new Forms.ToolStripMenuItem("退出");
        exitItem.Click += (_, _) => exitAction();
        menu.Items.Add(exitItem);

        _iconStream = System.Reflection.Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("WinFloat.Assets.WinFloat.ico");
        _applicationIcon = _iconStream is not null
            ? new Icon(_iconStream)
            : (Icon)SystemIcons.Application.Clone();

        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "WinFloat 性能监控",
            Icon = _applicationIcon,
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => _window.ToggleVisibility();
        _window.SettingsChanged += RefreshChecks;
        RefreshChecks();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _window.SettingsChanged -= RefreshChecks;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _applicationIcon.Dispose();
        _iconStream?.Dispose();
    }

    private void ToggleStartup()
    {
        var enabled = !_window.Settings.StartWithWindows;
        if (_startupService.SetEnabled(enabled))
        {
            _window.SetStartWithWindows(enabled);
            _settingsService.Save(_window.Settings);
        }
    }

    private void RefreshChecks()
    {
        if (_disposed)
            return;

        var settings = _window.Settings;
        _showMenuItem.Text = _window.IsVisible ? "隐藏悬浮窗" : "显示悬浮窗";
        _lockMenuItem.Checked = settings.LockWindow;
        _clickThroughMenuItem.Checked = settings.ClickThrough;
        _topmostMenuItem.Checked = settings.AlwaysOnTop;
        _startupMenuItem.Checked = settings.StartWithWindows;
    }
}
