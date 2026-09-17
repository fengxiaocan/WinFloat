using System.Text.Json.Serialization;

namespace WinFloat.Models;

public enum LayoutOrientation
{
    Vertical,
    Horizontal
}

public enum ThemeMode
{
    Dark,
    Light,
    System
}

public enum BackgroundMode
{
    Always,
    Transparent,
    Hover
}

public enum SpeedUnit
{
    Auto,
    Kilobytes,
    Megabytes
}

public enum DoubleClickAction
{
    OpenSettings,
    ToggleMinimalMode
}

public sealed class AppSettings
{
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }

    public LayoutOrientation Orientation { get; set; } = LayoutOrientation.Vertical;
    public ThemeMode Theme { get; set; } = ThemeMode.Dark;
    public BackgroundMode Background { get; set; } = BackgroundMode.Always;
    public SpeedUnit SpeedUnit { get; set; } = SpeedUnit.Auto;
    public DoubleClickAction DoubleClickAction { get; set; } = DoubleClickAction.OpenSettings;

    public int Opacity { get; set; } = 85;
    public int FontSize { get; set; } = 14;
    public bool RoundedCorners { get; set; } = true;
    public bool CompactMode { get; set; }
    public bool HighlightThresholds { get; set; } = true;

    public bool ShowDownload { get; set; } = true;
    public bool ShowUpload { get; set; } = true;
    public bool ShowCpuUsage { get; set; } = true;
    public bool ShowCpuTemperature { get; set; } = true;
    public bool ShowMemoryUsage { get; set; } = true;
    public bool ShowGpuUsage { get; set; } = true;
    public bool ShowGpuTemperature { get; set; } = true;

    public bool AlwaysOnTop { get; set; } = true;
    public bool ClickThrough { get; set; }
    public bool LockWindow { get; set; }
    public bool ShowOnStartup { get; set; } = true;
    public bool StartWithWindows { get; set; }

    public int RefreshIntervalMs { get; set; } = 1000;
    public string NetworkAdapterId { get; set; } = "Auto";
    public string GpuAdapterName { get; set; } = "Auto";

    [JsonIgnore]
    public bool HasSavedPosition => WindowLeft.HasValue && WindowTop.HasValue;

    public AppSettings Clone()
    {
        return new AppSettings
        {
            WindowLeft = WindowLeft,
            WindowTop = WindowTop,
            Orientation = Orientation,
            Theme = Theme,
            Background = Background,
            SpeedUnit = SpeedUnit,
            DoubleClickAction = DoubleClickAction,
            Opacity = Opacity,
            FontSize = FontSize,
            RoundedCorners = RoundedCorners,
            CompactMode = CompactMode,
            HighlightThresholds = HighlightThresholds,
            ShowDownload = ShowDownload,
            ShowUpload = ShowUpload,
            ShowCpuUsage = ShowCpuUsage,
            ShowCpuTemperature = ShowCpuTemperature,
            ShowMemoryUsage = ShowMemoryUsage,
            ShowGpuUsage = ShowGpuUsage,
            ShowGpuTemperature = ShowGpuTemperature,
            AlwaysOnTop = AlwaysOnTop,
            ClickThrough = ClickThrough,
            LockWindow = LockWindow,
            ShowOnStartup = ShowOnStartup,
            StartWithWindows = StartWithWindows,
            RefreshIntervalMs = RefreshIntervalMs,
            NetworkAdapterId = NetworkAdapterId,
            GpuAdapterName = GpuAdapterName
        };
    }

    public void CopyFrom(AppSettings source)
    {
        WindowLeft = source.WindowLeft;
        WindowTop = source.WindowTop;
        Orientation = source.Orientation;
        Theme = source.Theme;
        Background = source.Background;
        SpeedUnit = source.SpeedUnit;
        DoubleClickAction = source.DoubleClickAction;
        Opacity = source.Opacity;
        FontSize = source.FontSize;
        RoundedCorners = source.RoundedCorners;
        CompactMode = source.CompactMode;
        HighlightThresholds = source.HighlightThresholds;
        ShowDownload = source.ShowDownload;
        ShowUpload = source.ShowUpload;
        ShowCpuUsage = source.ShowCpuUsage;
        ShowCpuTemperature = source.ShowCpuTemperature;
        ShowMemoryUsage = source.ShowMemoryUsage;
        ShowGpuUsage = source.ShowGpuUsage;
        ShowGpuTemperature = source.ShowGpuTemperature;
        AlwaysOnTop = source.AlwaysOnTop;
        ClickThrough = source.ClickThrough;
        LockWindow = source.LockWindow;
        ShowOnStartup = source.ShowOnStartup;
        StartWithWindows = source.StartWithWindows;
        RefreshIntervalMs = source.RefreshIntervalMs;
        NetworkAdapterId = source.NetworkAdapterId;
        GpuAdapterName = source.GpuAdapterName;
    }
}

public sealed class NetworkAdapterOption
{
    public NetworkAdapterOption(string id, string displayName)
    {
        Id = id;
        DisplayName = displayName;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public override string ToString() => DisplayName;
}

public sealed class GpuAdapterOption
{
    public GpuAdapterOption(string name, string displayName)
    {
        Name = name;
        DisplayName = displayName;
    }

    public string Name { get; }
    public string DisplayName { get; }
    public override string ToString() => DisplayName;
}
