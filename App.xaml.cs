using System.Windows;
using WinFloat.Models;
using WinFloat.Services;
using WinFloat.Views;

namespace WinFloat;

public partial class App : System.Windows.Application
{
    public static AppSettings Settings { get; private set; } = new();
    public static SettingsService SettingsService { get; private set; } = null!;
    public static MonitorService MonitorService { get; private set; } = null!;
    public static StartupService StartupService { get; private set; } = null!;
    public new static MainWindow MainWindow { get; private set; } = null!;

    internal static bool IsExiting { get; private set; }

    private TrayIconService? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        Services.NativeMethods.TrySetPerMonitorDpiAwareness();
        base.OnStartup(e);

        SettingsService = new SettingsService();
        Settings = SettingsService.Load();
        StartupService = new StartupService();
        Settings.StartWithWindows = StartupService.IsEnabled();

        MonitorService = new MonitorService(Settings, SettingsService);
        MainWindow = new MainWindow(Settings, SettingsService, MonitorService, StartupService);
        _tray = new TrayIconService(MainWindow, StartupService, SettingsService, ExitApplication);

        MainWindow.Show();
        if (!Settings.ShowOnStartup)
            MainWindow.Hide();

        MonitorService.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        IsExiting = true;
        MainWindow?.SavePosition();
        MonitorService?.Dispose();
        _tray?.Dispose();
        if (SettingsService is not null)
            SettingsService.Save(Settings);
        base.OnExit(e);
    }

    private void ExitApplication()
    {
        IsExiting = true;
        Shutdown();
    }
}
