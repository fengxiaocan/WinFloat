namespace WinFloat.Models;

public sealed record MonitorSnapshot(
    DateTime Timestamp,
    double? DownloadBytesPerSecond,
    double? UploadBytesPerSecond,
    double? CpuUsage,
    double? MemoryUsage,
    double? GpuUsage,
    double? CpuTemperature,
    double? GpuTemperature,
    string? GpuName);
