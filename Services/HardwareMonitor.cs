using LibreHardwareMonitor.Hardware;
using WinFloat.Models;

namespace WinFloat.Services;

public sealed class HardwareMonitor : IDisposable
{
    private readonly object _sync = new();
    private readonly Computer _computer;
    private readonly UpdateVisitor _visitor = new();
    private bool _opened;
    private bool _disposed;

    public HardwareMonitor(SettingsService settingsService)
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMotherboardEnabled = true
        };

        try
        {
            _computer.Open();
            _opened = true;
        }
        catch (Exception exception)
        {
            settingsService.LogError("硬件监控初始化失败，温度和 GPU 传感器将不可用", exception);
        }
    }

    public HardwareReadout Read(string gpuSelection)
    {
        lock (_sync)
        {
            if (_disposed || !_opened)
                return HardwareReadout.Empty;

            try
            {
                _computer.Accept(_visitor);

                var hardware = EnumerateHardware(_computer.Hardware).ToList();
                var cpu = hardware.FirstOrDefault(item => item.HardwareType == HardwareType.Cpu);
                var gpus = hardware.Where(IsGpu).ToList();
                var gpu = SelectGpu(gpus, gpuSelection);

                return new HardwareReadout(
                    PickTemperature(cpu, false),
                    PickLoad(gpu),
                    PickTemperature(gpu, true),
                    gpu?.Name);
            }
            catch
            {
                // Sensor access is optional. Keep the rest of the monitor alive.
                return HardwareReadout.Empty;
            }
        }
    }

    public IReadOnlyList<GpuAdapterOption> GetGpuOptions()
    {
        lock (_sync)
        {
            if (_disposed || !_opened)
                return new[] { new GpuAdapterOption("Auto", "自动选择主要 GPU") };

            try
            {
                var options = new List<GpuAdapterOption>
                {
                    new("Auto", "自动选择主要 GPU")
                };

                foreach (var gpu in EnumerateHardware(_computer.Hardware).Where(IsGpu))
                    options.Add(new GpuAdapterOption(gpu.Name, gpu.Name));

                return options;
            }
            catch
            {
                return new[] { new GpuAdapterOption("Auto", "自动选择主要 GPU") };
            }
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;

            _disposed = true;
            try
            {
                if (_opened)
                    _computer.Close();
            }
            catch
            {
                // Best effort during shutdown.
            }
        }
    }

    private static IEnumerable<IHardware> EnumerateHardware(IEnumerable<IHardware> hardware)
    {
        foreach (var item in hardware)
        {
            yield return item;
            foreach (var child in EnumerateHardware(item.SubHardware))
                yield return child;
        }
    }

    private static bool IsGpu(IHardware hardware)
    {
        return hardware.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel;
    }

    private static IHardware? SelectGpu(IReadOnlyList<IHardware> gpus, string gpuSelection)
    {
        if (gpus.Count == 0)
            return null;

        if (!string.IsNullOrWhiteSpace(gpuSelection) && !gpuSelection.Equals("Auto", StringComparison.OrdinalIgnoreCase))
        {
            var manual = gpus.FirstOrDefault(item => item.Name.Equals(gpuSelection, StringComparison.OrdinalIgnoreCase));
            if (manual is not null)
                return manual;
        }

        return gpus
            .Select(item => new { Hardware = item, Load = PickLoad(item) ?? 0 })
            .OrderByDescending(item => item.Load)
            .ThenBy(item => item.Hardware.Name)
            .First().Hardware;
    }

    private static double? PickLoad(IHardware? hardware)
    {
        if (hardware is null)
            return null;

        var sensors = hardware.Sensors
            .Where(sensor => sensor.SensorType == SensorType.Load && sensor.Value.HasValue)
            .Where(sensor => sensor.Value is >= 0 and <= 100)
            .ToList();

        if (sensors.Count == 0)
            return null;

        var preferred = sensors.FirstOrDefault(sensor =>
            sensor.Name.Contains("GPU Core", StringComparison.OrdinalIgnoreCase) ||
            sensor.Name.Contains("3D", StringComparison.OrdinalIgnoreCase) ||
            sensor.Name.Contains("D3D", StringComparison.OrdinalIgnoreCase));

        return preferred?.Value ?? sensors.Max(sensor => sensor.Value!.Value);
    }

    private static double? PickTemperature(IHardware? hardware, bool gpu)
    {
        if (hardware is null)
            return null;

        var sensors = hardware.Sensors
            .Where(sensor => sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue)
            .Where(sensor => sensor.Value is > 0 and < 130)
            .ToList();

        if (sensors.Count == 0)
            return null;

        var preferred = gpu
            ? sensors.FirstOrDefault(sensor =>
                sensor.Name.Contains("GPU Core", StringComparison.OrdinalIgnoreCase) ||
                sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase))
            : sensors.FirstOrDefault(sensor =>
                sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
                sensor.Name.Contains("Tctl", StringComparison.OrdinalIgnoreCase) ||
                sensor.Name.Contains("Tdie", StringComparison.OrdinalIgnoreCase));

        return preferred?.Value ?? sensors.First().Value;
    }

    private sealed class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer) => computer.Traverse(this);

        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (var subHardware in hardware.SubHardware)
                subHardware.Accept(this);
        }

        public void VisitSensor(ISensor sensor)
        {
        }

        public void VisitParameter(IParameter parameter)
        {
        }
    }

    public sealed record HardwareReadout(
        double? CpuTemperature,
        double? GpuUsage,
        double? GpuTemperature,
        string? GpuName)
    {
        public static HardwareReadout Empty { get; } = new(null, null, null, null);
    }
}
