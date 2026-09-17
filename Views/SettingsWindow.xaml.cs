using System.Windows;
using WinFloat.Models;
using WinFloat.Services;
using WpfControls = System.Windows.Controls;

namespace WinFloat.Views;

public partial class SettingsWindow : Window
{
    private readonly MonitorService _monitorService;
    private readonly StartupService _startupService;
    private readonly AppSettings _draft;

    public SettingsWindow(
        AppSettings settings,
        MonitorService monitorService,
        StartupService startupService)
    {
        InitializeComponent();
        _draft = settings.Clone();
        _monitorService = monitorService;
        _startupService = startupService;
        Loaded += SettingsWindow_OnLoaded;
    }

    public event Action<AppSettings>? SettingsSaved;

    private void SettingsWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        StartWithWindowsCheckBox.IsChecked = _draft.StartWithWindows;
        ShowOnStartupCheckBox.IsChecked = _draft.ShowOnStartup;
        AlwaysOnTopCheckBox.IsChecked = _draft.AlwaysOnTop;
        LockWindowCheckBox.IsChecked = _draft.LockWindow;
        ClickThroughCheckBox.IsChecked = _draft.ClickThrough;

        SelectByTag(DoubleClickComboBox, _draft.DoubleClickAction.ToString());
        SelectByTag(OrientationComboBox, _draft.Orientation.ToString());
        SelectByTag(ThemeComboBox, _draft.Theme.ToString());
        SelectByTag(BackgroundComboBox, _draft.Background.ToString());
        SelectByTag(SpeedUnitComboBox, _draft.SpeedUnit.ToString());
        SelectByTag(FontSizeComboBox, _draft.FontSize.ToString());
        SelectByTag(RefreshIntervalComboBox, _draft.RefreshIntervalMs.ToString());

        OpacitySlider.Value = Math.Clamp(_draft.Opacity, 40, 100);
        OpacityValueText.Text = $"{(int)OpacitySlider.Value}%";
        RoundedCornersCheckBox.IsChecked = _draft.RoundedCorners;
        CompactModeCheckBox.IsChecked = _draft.CompactMode;
        HighlightThresholdsCheckBox.IsChecked = _draft.HighlightThresholds;

        ShowDownloadCheckBox.IsChecked = _draft.ShowDownload;
        ShowUploadCheckBox.IsChecked = _draft.ShowUpload;
        ShowCpuUsageCheckBox.IsChecked = _draft.ShowCpuUsage;
        ShowCpuTemperatureCheckBox.IsChecked = _draft.ShowCpuTemperature;
        ShowMemoryUsageCheckBox.IsChecked = _draft.ShowMemoryUsage;
        ShowGpuUsageCheckBox.IsChecked = _draft.ShowGpuUsage;
        ShowGpuTemperatureCheckBox.IsChecked = _draft.ShowGpuTemperature;

        NetworkAdapterComboBox.ItemsSource = NetworkMonitor.GetAdapters();
        if (NetworkAdapterComboBox.Items.Count == 0 || NetworkAdapterComboBox.SelectedValue is null)
            NetworkAdapterComboBox.SelectedValue = "Auto";
        NetworkAdapterComboBox.SelectedValue = _draft.NetworkAdapterId;
        if (NetworkAdapterComboBox.SelectedIndex < 0)
            NetworkAdapterComboBox.SelectedValue = "Auto";

        GpuAdapterComboBox.ItemsSource = _monitorService.GetGpuOptions();
        GpuAdapterComboBox.SelectedValue = _draft.GpuAdapterName;
        if (GpuAdapterComboBox.SelectedIndex < 0)
            GpuAdapterComboBox.SelectedValue = "Auto";
    }

    private void OpacitySlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OpacityValueText is not null)
            OpacityValueText.Text = $"{(int)e.NewValue}%";
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        ReadControls();

        if (!_startupService.SetEnabled(_draft.StartWithWindows))
        {
            System.Windows.MessageBox.Show(
                "无法写入用户级开机启动项，其他设置仍可保存。",
                "WinFloat",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            _draft.StartWithWindows = false;
        }

        SettingsSaved?.Invoke(_draft);
        DialogResult = true;
        Close();
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ReadControls()
    {
        _draft.StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;
        _draft.ShowOnStartup = ShowOnStartupCheckBox.IsChecked == true;
        _draft.AlwaysOnTop = AlwaysOnTopCheckBox.IsChecked == true;
        _draft.LockWindow = LockWindowCheckBox.IsChecked == true;
        _draft.ClickThrough = ClickThroughCheckBox.IsChecked == true;

        _draft.DoubleClickAction = ParseTag<DoubleClickAction>(DoubleClickComboBox, DoubleClickAction.OpenSettings);
        _draft.Orientation = ParseTag<LayoutOrientation>(OrientationComboBox, LayoutOrientation.Vertical);
        _draft.Theme = ParseTag<ThemeMode>(ThemeComboBox, ThemeMode.Dark);
        _draft.Background = ParseTag<BackgroundMode>(BackgroundComboBox, BackgroundMode.Always);
        _draft.SpeedUnit = ParseTag<SpeedUnit>(SpeedUnitComboBox, SpeedUnit.Auto);
        _draft.FontSize = ParseTag(FontSizeComboBox, 14);
        _draft.RefreshIntervalMs = ParseTag(RefreshIntervalComboBox, 1000);

        _draft.Opacity = (int)Math.Round(OpacitySlider.Value);
        _draft.RoundedCorners = RoundedCornersCheckBox.IsChecked == true;
        _draft.CompactMode = CompactModeCheckBox.IsChecked == true;
        _draft.HighlightThresholds = HighlightThresholdsCheckBox.IsChecked == true;

        _draft.ShowDownload = ShowDownloadCheckBox.IsChecked == true;
        _draft.ShowUpload = ShowUploadCheckBox.IsChecked == true;
        _draft.ShowCpuUsage = ShowCpuUsageCheckBox.IsChecked == true;
        _draft.ShowCpuTemperature = ShowCpuTemperatureCheckBox.IsChecked == true;
        _draft.ShowMemoryUsage = ShowMemoryUsageCheckBox.IsChecked == true;
        _draft.ShowGpuUsage = ShowGpuUsageCheckBox.IsChecked == true;
        _draft.ShowGpuTemperature = ShowGpuTemperatureCheckBox.IsChecked == true;

        _draft.NetworkAdapterId = NetworkAdapterComboBox.SelectedValue as string ?? "Auto";
        _draft.GpuAdapterName = GpuAdapterComboBox.SelectedValue as string ?? "Auto";
    }

    private static void SelectByTag(System.Windows.Controls.ComboBox comboBox, string tag)
    {
        foreach (var item in comboBox.Items.OfType<WpfControls.ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }
    }

    private static T ParseTag<T>(System.Windows.Controls.ComboBox comboBox, T fallback) where T : struct, Enum
    {
        var tag = (comboBox.SelectedItem as WpfControls.ComboBoxItem)?.Tag?.ToString();
        return Enum.TryParse<T>(tag, true, out var value) ? value : fallback;
    }

    private static int ParseTag(System.Windows.Controls.ComboBox comboBox, int fallback)
    {
        var tag = (comboBox.SelectedItem as WpfControls.ComboBoxItem)?.Tag?.ToString();
        return int.TryParse(tag, out var value) ? value : fallback;
    }
}
