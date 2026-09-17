using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Forms = System.Windows.Forms;
using Color = System.Windows.Media.Color;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using WinFloat.Models;
using WinFloat.Services;

namespace WinFloat.Views;

public partial class MainWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly MonitorService _monitorService;
    private readonly StartupService _startupService;
    private IntPtr _handle;
    private bool _isDragging;
    private bool _isHovering;
    private bool _hasPositioned;

    public MainWindow(
        AppSettings settings,
        SettingsService settingsService,
        MonitorService monitorService,
        StartupService startupService)
    {
        InitializeComponent();
        Settings = settings;
        _settingsService = settingsService;
        _monitorService = monitorService;
        _startupService = startupService;
        _monitorService.SnapshotUpdated += OnSnapshotUpdated;
        ApplySettings(raiseChanged: false);
    }

    public AppSettings Settings { get; private set; }
    public event Action? SettingsChanged;

    protected override void OnClosed(EventArgs e)
    {
        _monitorService.SnapshotUpdated -= OnSnapshotUpdated;
        base.OnClosed(e);
    }

    public void ToggleVisibility()
    {
        if (IsVisible)
            Hide();
        else
        {
            Show();
            ActivateSafely();
        }
    }

    public void OpenSettings()
    {
        var dialog = new SettingsWindow(Settings, _monitorService, _startupService)
        {
            Owner = this
        };

        dialog.SettingsSaved += OnSettingsSaved;
        dialog.ShowDialog();
        dialog.SettingsSaved -= OnSettingsSaved;
    }

    public void SetLockWindow(bool value)
    {
        Settings.LockWindow = value;
        SaveAndApply();
    }

    public void SetClickThrough(bool value)
    {
        Settings.ClickThrough = value;
        SaveAndApply();
    }

    public void SetAlwaysOnTop(bool value)
    {
        Settings.AlwaysOnTop = value;
        SaveAndApply();
    }

    public void SetStartWithWindows(bool value)
    {
        Settings.StartWithWindows = value;
        SettingsChanged?.Invoke();
    }

    public void SavePosition()
    {
        if (!IsLoaded || double.IsNaN(Left) || double.IsNaN(Top))
            return;

        Settings.WindowLeft = Left;
        Settings.WindowTop = Top;
        _settingsService.Save(Settings);
    }

    private void Window_OnSourceInitialized(object? sender, EventArgs e)
    {
        _handle = new WindowInteropHelper(this).Handle;
        ApplyWindowStyles();
    }

    private void Window_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_hasPositioned)
        {
            _hasPositioned = true;
            PositionWindow();
        }
    }

    private void Window_OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!App.IsExiting)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        SavePosition();
    }

    private void Window_OnLocationChanged(object? sender, EventArgs e)
    {
        if (_isDragging)
            ClampToCurrentScreen();
    }

    private void RootBorder_OnMouseEnter(object sender, MouseEventArgs e)
    {
        _isHovering = true;
        ApplyBackground();
    }

    private void RootBorder_OnMouseLeave(object sender, MouseEventArgs e)
    {
        _isHovering = false;
        ApplyBackground();
    }

    private void RootBorder_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Settings.ClickThrough)
            return;

        if (e.ClickCount > 1)
        {
            RootBorder_OnMouseDoubleClick(sender, e);
            e.Handled = true;
            return;
        }

        if (Settings.LockWindow)
            return;

        _isDragging = true;
        try
        {
            DragMove();
            SavePosition();
        }
        catch (InvalidOperationException)
        {
            // DragMove can fail if the pointer leaves during a DPI transition.
        }
        finally
        {
            _isDragging = false;
        }
    }

    private void RootBorder_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
            SavePosition();
    }

    private void RootBorder_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Settings.ClickThrough || e.ChangedButton != MouseButton.Left)
            return;

        if (Settings.DoubleClickAction == DoubleClickAction.OpenSettings)
            OpenSettings();
        else
        {
            Settings.CompactMode = !Settings.CompactMode;
            SaveAndApply();
        }
    }

    private void OnSnapshotUpdated(MonitorSnapshot snapshot)
    {
        Dispatcher.BeginInvoke(() => UpdateSnapshot(snapshot));
    }

    private void OnSettingsSaved(AppSettings settings)
    {
        Settings = settings;
        ApplySettings();
        _monitorService.UpdateSettings(Settings);
        _settingsService.Save(Settings);
    }

    private void SaveAndApply()
    {
        ApplySettings();
        _monitorService.UpdateSettings(Settings);
        _settingsService.Save(Settings);
    }

    private void ApplySettings(bool raiseChanged = true)
    {
        Topmost = Settings.AlwaysOnTop;
        ApplyWindowStyles();
        ApplyVisualStyle();
        ApplyLayout();
        if (raiseChanged)
            SettingsChanged?.Invoke();
    }

    private void ApplyWindowStyles()
    {
        if (_handle == IntPtr.Zero)
            return;

        var styles = NativeMethods.GetWindowLongPtr(_handle, NativeMethods.GwlExStyle).ToInt64();
        styles |= NativeMethods.WsExToolWindow | NativeMethods.WsExNoActivate | NativeMethods.WsExLayered;
        styles &= ~NativeMethods.WsExAppWindow;
        if (Settings.ClickThrough)
            styles |= NativeMethods.WsExTransparent;
        else
            styles &= ~NativeMethods.WsExTransparent;

        NativeMethods.SetWindowLongPtr(_handle, NativeMethods.GwlExStyle, new IntPtr(styles));
    }

    private void ApplyVisualStyle()
    {
        var dark = Settings.Theme != ThemeMode.Light;
        var foreground = dark ? Color.FromRgb(242, 245, 247) : Color.FromRgb(28, 33, 40);
        var secondary = dark ? Color.FromRgb(176, 187, 199) : Color.FromRgb(93, 105, 119);

        var border = dark ? Color.FromRgb(72, 82, 96) : Color.FromRgb(195, 202, 212);
        RootBorder.BorderBrush = new SolidColorBrush(border);
        RootBorder.CornerRadius = Settings.RoundedCorners ? new CornerRadius(6) : new CornerRadius(0);

        foreach (var textBlock in FindTextBlocks(RootBorder))
        {
            textBlock.Foreground = new SolidColorBrush(textBlock.Name.Contains("Temperature", StringComparison.Ordinal)
                ? secondary
                : foreground);
            textBlock.FontSize = Math.Clamp(Settings.FontSize, 10, 32);
            textBlock.FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        }

        ApplyBackground();
    }

    private void ApplyBackground()
    {
        var dark = Settings.Theme != ThemeMode.Light;
        var baseColor = dark ? Color.FromRgb(24, 28, 34) : Color.FromRgb(248, 250, 252);
        var alpha = Settings.Background switch
        {
            BackgroundMode.Transparent => (byte)0,
            BackgroundMode.Hover when !_isHovering => (byte)0,
            _ => (byte)Math.Clamp(Settings.Opacity * 255 / 100, 0, 255)
        };

        RootBorder.Background = new SolidColorBrush(Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B));
    }

    private void ApplyLayout()
    {
        VerticalPanel.Visibility = Settings.Orientation == LayoutOrientation.Vertical
            ? Visibility.Visible
            : Visibility.Collapsed;
        HorizontalPanel.Visibility = Settings.Orientation == LayoutOrientation.Horizontal
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (IsLoaded)
            ClampToCurrentScreen();
    }

    private void UpdateSnapshot(MonitorSnapshot snapshot)
    {
        var settings = Settings;
        var download = MetricFormatter.FormatSpeed(snapshot.DownloadBytesPerSecond, settings.SpeedUnit);
        var upload = MetricFormatter.FormatSpeed(snapshot.UploadBytesPerSecond, settings.SpeedUnit);
        var cpuUsage = MetricFormatter.FormatPercent(snapshot.CpuUsage);
        var memory = MetricFormatter.FormatPercent(snapshot.MemoryUsage);
        var gpuUsage = MetricFormatter.FormatPercent(snapshot.GpuUsage);
        var cpuTemp = MetricFormatter.FormatTemperature(snapshot.CpuTemperature);
        var gpuTemp = MetricFormatter.FormatTemperature(snapshot.GpuTemperature);

        VerticalDownloadText.Text = $"↓ {download}";
        VerticalUploadText.Text = $"↑ {upload}";
        VerticalCpuUsageText.Text = $"CPU {cpuUsage}";
        VerticalCpuTemperatureText.Text = cpuTemp;
        VerticalMemoryText.Text = $"RAM {memory}";
        VerticalGpuUsageText.Text = $"GPU {gpuUsage}";
        VerticalGpuTemperatureText.Text = gpuTemp;

        HorizontalDownloadText.Text = $"↓ {download}";
        HorizontalUploadText.Text = $"↑ {upload}";
        HorizontalCpuUsageText.Text = $"CPU {cpuUsage}";
        HorizontalCpuTemperatureText.Text = cpuTemp;
        HorizontalMemoryText.Text = $"RAM {memory}";
        HorizontalGpuUsageText.Text = $"GPU {gpuUsage}";
        HorizontalGpuTemperatureText.Text = gpuTemp;

        SetMetricVisibility(settings, snapshot);
        ApplyMetricColors(settings, snapshot);
    }

    private void SetMetricVisibility(AppSettings settings, MonitorSnapshot snapshot)
    {
        var showCpuTemp = !settings.CompactMode && settings.ShowCpuTemperature && snapshot.CpuTemperature.HasValue;
        var showGpuTemp = !settings.CompactMode && settings.ShowGpuTemperature && snapshot.GpuTemperature.HasValue;
        var showDownload = settings.ShowDownload;
        var showUpload = settings.ShowUpload;
        var showCpuUsage = settings.ShowCpuUsage;
        var showMemory = settings.ShowMemoryUsage;
        var showGpuUsage = settings.ShowGpuUsage && snapshot.GpuUsage.HasValue;

        VerticalDownloadText.Visibility = showDownload ? Visibility.Visible : Visibility.Collapsed;
        VerticalUploadText.Visibility = showUpload ? Visibility.Visible : Visibility.Collapsed;
        VerticalNetworkRow.Visibility = showDownload || showUpload ? Visibility.Visible : Visibility.Collapsed;
        VerticalCpuUsageText.Visibility = showCpuUsage ? Visibility.Visible : Visibility.Collapsed;
        VerticalCpuTemperatureText.Visibility = showCpuTemp ? Visibility.Visible : Visibility.Collapsed;
        VerticalCpuRow.Visibility = showCpuUsage || showCpuTemp ? Visibility.Visible : Visibility.Collapsed;
        VerticalMemoryText.Visibility = showMemory ? Visibility.Visible : Visibility.Collapsed;
        VerticalGpuUsageText.Visibility = showGpuUsage ? Visibility.Visible : Visibility.Collapsed;
        VerticalGpuTemperatureText.Visibility = showGpuTemp ? Visibility.Visible : Visibility.Collapsed;
        VerticalGpuRow.Visibility = showGpuUsage || showGpuTemp ? Visibility.Visible : Visibility.Collapsed;

        HorizontalDownloadText.Visibility = showDownload ? Visibility.Visible : Visibility.Collapsed;
        HorizontalUploadText.Visibility = showUpload ? Visibility.Visible : Visibility.Collapsed;
        HorizontalCpuUsageText.Visibility = showCpuUsage ? Visibility.Visible : Visibility.Collapsed;
        HorizontalCpuTemperatureText.Visibility = showCpuTemp ? Visibility.Visible : Visibility.Collapsed;
        HorizontalMemoryText.Visibility = showMemory ? Visibility.Visible : Visibility.Collapsed;
        HorizontalGpuUsageText.Visibility = showGpuUsage ? Visibility.Visible : Visibility.Collapsed;
        HorizontalGpuTemperatureText.Visibility = showGpuTemp ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyMetricColors(AppSettings settings, MonitorSnapshot snapshot)
    {
        var dark = settings.Theme != ThemeMode.Light;
        var normal = dark ? Color.FromRgb(242, 245, 247) : Color.FromRgb(28, 33, 40);
        var secondary = dark ? Color.FromRgb(176, 187, 199) : Color.FromRgb(93, 105, 119);
        var high = Color.FromRgb(255, 190, 92);
        var critical = Color.FromRgb(255, 112, 112);

        SetBrush(normal, VerticalDownloadText, VerticalUploadText, HorizontalDownloadText, HorizontalUploadText);
        SetBrush(settings.HighlightThresholds ? ColorFor(snapshot.CpuUsage, 70, 90, normal, high, critical) : normal,
            VerticalCpuUsageText, HorizontalCpuUsageText);
        SetBrush(settings.HighlightThresholds ? ColorFor(snapshot.MemoryUsage, 80, 92, normal, high, critical) : normal,
            VerticalMemoryText, HorizontalMemoryText);
        SetBrush(settings.HighlightThresholds ? ColorFor(snapshot.GpuUsage, 80, 95, normal, high, critical) : normal,
            VerticalGpuUsageText, HorizontalGpuUsageText);
        SetBrush(settings.HighlightThresholds ? ColorFor(snapshot.CpuTemperature, 75, 85, secondary, high, critical) : secondary,
            VerticalCpuTemperatureText, HorizontalCpuTemperatureText);
        SetBrush(settings.HighlightThresholds ? ColorFor(snapshot.GpuTemperature, 75, 85, secondary, high, critical) : secondary,
            VerticalGpuTemperatureText, HorizontalGpuTemperatureText);
    }

    private static Color ColorFor(double? value, double highThreshold, double criticalThreshold, Color normal, Color high, Color critical)
    {
        if (!value.HasValue)
            return normal;
        return value.Value >= criticalThreshold ? critical : value.Value >= highThreshold ? high : normal;
    }

    private static void SetBrush(Color color, params System.Windows.Controls.TextBlock[] textBlocks)
    {
        var brush = new SolidColorBrush(color);
        foreach (var textBlock in textBlocks)
            textBlock.Foreground = brush;
    }

    private static IEnumerable<System.Windows.Controls.TextBlock> FindTextBlocks(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is System.Windows.Controls.TextBlock textBlock)
                yield return textBlock;

            foreach (var nested in FindTextBlocks(child))
                yield return nested;
        }
    }

    private void PositionWindow()
    {
        if (Settings.HasSavedPosition)
        {
            Left = Settings.WindowLeft!.Value;
            Top = Settings.WindowTop!.Value;
            ClampToCurrentScreen();
            return;
        }

        var area = Forms.Screen.PrimaryScreen?.WorkingArea;
        if (area is null)
            return;

        var scale = GetDpiScale();
        Left = area.Value.Right / scale - ActualWidth - 24;
        Top = area.Value.Top / scale + 24;
        ClampToCurrentScreen();
    }

    private void ClampToCurrentScreen()
    {
        if (_handle == IntPtr.Zero || double.IsNaN(ActualWidth) || ActualWidth <= 0)
            return;

        var screen = Forms.Screen.FromHandle(_handle);
        var scale = GetDpiScale();
        var left = screen.WorkingArea.Left / scale;
        var top = screen.WorkingArea.Top / scale;
        var right = screen.WorkingArea.Right / scale;
        var bottom = screen.WorkingArea.Bottom / scale;

        Left = Math.Clamp(Left, left, Math.Max(left, right - ActualWidth));
        Top = Math.Clamp(Top, top, Math.Max(top, bottom - ActualHeight));
    }

    private double GetDpiScale()
    {
        if (_handle == IntPtr.Zero)
            return 1;

        var dpi = NativeMethods.GetDpiForWindow(_handle);
        return dpi > 0 ? dpi / 96d : 1;
    }

    private void ActivateSafely()
    {
        if (Settings.ClickThrough)
            return;
        // The overlay intentionally remains non-activating; showing it is sufficient.
    }
}

internal static class MetricFormatter
{
    public static string FormatPercent(double? value) => value.HasValue ? $"{Math.Round(value.Value):0}%" : "--";

    public static string FormatTemperature(double? value)
    {
        return value.HasValue ? $"{Math.Round(value.Value):0}°C" : string.Empty;
    }

    public static string FormatSpeed(double? bytesPerSecond, SpeedUnit unit)
    {
        if (!bytesPerSecond.HasValue)
            return "--";

        var value = Math.Max(0, bytesPerSecond.Value);
        if (unit == SpeedUnit.Kilobytes)
            return FormatValue(value / 1024, "KB/s");
        if (unit == SpeedUnit.Megabytes)
            return FormatValue(value / (1024 * 1024), "MB/s");

        if (value < 1024)
            return $"{value:0} B/s";
        if (value < 1024 * 1024)
            return FormatValue(value / 1024, "KB/s");
        if (value < 1024 * 1024 * 1024)
            return FormatValue(value / (1024 * 1024), "MB/s");
        return FormatValue(value / (1024 * 1024 * 1024), "GB/s");
    }

    private static string FormatValue(double value, string suffix)
    {
        var format = value < 10 ? "0.0" : "0";
        return $"{value.ToString(format, System.Globalization.CultureInfo.InvariantCulture)} {suffix}";
    }
}
