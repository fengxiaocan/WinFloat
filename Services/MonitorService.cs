using WinFloat.Models;

namespace WinFloat.Services;

public sealed class MonitorService : IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly CpuUsageReader _cpu = new();
    private readonly MemoryUsageReader _memory = new();
    private readonly NetworkMonitor _network = new();
    private readonly HardwareMonitor _hardware;
    private readonly object _settingsSync = new();
    private AppSettings _settings;
    private CancellationTokenSource? _cancellation;
    private Task? _loopTask;
    private bool _disposed;

    public MonitorService(AppSettings settings, SettingsService settingsService)
    {
        _settings = settings;
        _settingsService = settingsService;
        _hardware = new HardwareMonitor(settingsService);
    }

    public event Action<MonitorSnapshot>? SnapshotUpdated;

    public void UpdateSettings(AppSettings settings)
    {
        lock (_settingsSync)
            _settings = settings;
    }

    public IReadOnlyList<GpuAdapterOption> GetGpuOptions() => _hardware.GetGpuOptions();

    public void Start()
    {
        if (_loopTask is not null || _disposed)
            return;

        _cancellation = new CancellationTokenSource();
        _loopTask = Task.Run(() => RunAsync(_cancellation.Token));
    }

    public async Task StopAsync()
    {
        if (_cancellation is null)
            return;

        _cancellation.Cancel();
        if (_loopTask is not null)
        {
            try
            {
                await _loopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _cancellation.Dispose();
        _cancellation = null;
        _loopTask = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        StopAsync().GetAwaiter().GetResult();
        _hardware.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var startedAt = DateTime.UtcNow;
            try
            {
                var snapshot = ReadSnapshot();
                SnapshotUpdated?.Invoke(snapshot);
            }
            catch (Exception exception)
            {
                _settingsService.LogError("监控采样失败", exception);
            }

            var settings = GetSettingsSnapshot();
            var elapsed = DateTime.UtcNow - startedAt;
            var delay = Math.Max(100, settings.RefreshIntervalMs) - (int)elapsed.TotalMilliseconds;
            if (delay > 0)
            {
                try
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private MonitorSnapshot ReadSnapshot()
    {
        var settings = GetSettingsSnapshot();
        var network = _network.Read(settings.NetworkAdapterId);
        var hardware = _hardware.Read(settings.GpuAdapterName);

        return new MonitorSnapshot(
            DateTime.Now,
            network.DownloadBytesPerSecond,
            network.UploadBytesPerSecond,
            SafeRead(_cpu.Read),
            SafeRead(_memory.Read),
            hardware.GpuUsage,
            hardware.CpuTemperature,
            hardware.GpuTemperature,
            hardware.GpuName);
    }

    private AppSettings GetSettingsSnapshot()
    {
        lock (_settingsSync)
            return _settings.Clone();
    }

    private static double? SafeRead(Func<double?> read)
    {
        try
        {
            return read();
        }
        catch
        {
            return null;
        }
    }
}
