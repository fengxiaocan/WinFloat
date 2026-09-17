using System.Net.NetworkInformation;
using WinFloat.Models;

namespace WinFloat.Services;

public sealed class NetworkMonitor
{
    private readonly Dictionary<string, NetworkCounters> _lastCounters = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<double> _downloadSamples = new();
    private readonly Queue<double> _uploadSamples = new();
    private DateTime _lastSampleAt;
    private bool _hasSample;

    public NetworkSample Read(string adapterId)
    {
        var now = DateTime.UtcNow;
        var current = CollectCounters();

        if (!_hasSample)
        {
            _lastCounters.Clear();
            foreach (var pair in current)
                _lastCounters[pair.Key] = pair.Value;

            _lastSampleAt = now;
            _hasSample = true;
            return NetworkSample.Empty;
        }

        var elapsedSeconds = Math.Max((now - _lastSampleAt).TotalSeconds, 0.1);
        var rates = new Dictionary<string, NetworkRate>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in current)
        {
            if (!_lastCounters.TryGetValue(pair.Key, out var previous))
                continue;

            var received = pair.Value.BytesReceived >= previous.BytesReceived
                ? pair.Value.BytesReceived - previous.BytesReceived
                : 0;
            var sent = pair.Value.BytesSent >= previous.BytesSent
                ? pair.Value.BytesSent - previous.BytesSent
                : 0;

            rates[pair.Key] = new NetworkRate(received / elapsedSeconds, sent / elapsedSeconds);
        }

        var selected = SelectAdapter(adapterId, current, rates);
        var download = selected is null ? 0 : rates[selected].DownloadBytesPerSecond;
        var upload = selected is null ? 0 : rates[selected].UploadBytesPerSecond;

        _lastCounters.Clear();
        foreach (var pair in current)
            _lastCounters[pair.Key] = pair.Value;
        _lastSampleAt = now;

        return new NetworkSample(
            AddSmoothed(_downloadSamples, download),
            AddSmoothed(_uploadSamples, upload),
            selected);
    }

    public static IReadOnlyList<NetworkAdapterOption> GetAdapters()
    {
        var result = new List<NetworkAdapterOption>
        {
            new("Auto", "自动选择有效网卡")
        };

        try
        {
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces()
                         .Where(item => item.OperationalStatus != OperationalStatus.Unknown)
                         .OrderBy(item => item.Name))
            {
                if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                result.Add(new NetworkAdapterOption(
                    adapter.Id,
                    $"{adapter.Name} ({adapter.Description})"));
            }
        }
        catch
        {
            // The automatic mode remains available even when adapter enumeration fails.
        }

        return result;
    }

    private static Dictionary<string, NetworkCounters> CollectCounters()
    {
        var counters = new Dictionary<string, NetworkCounters>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up ||
                    adapter.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                    continue;

                if (LooksVirtual(adapter))
                    continue;

                try
                {
                    var statistics = adapter.GetIPStatistics();
                    counters[adapter.Id] = new NetworkCounters(
                        statistics.BytesReceived,
                        statistics.BytesSent,
                        adapter.Speed);
                }
                catch
                {
                    // Individual adapters can disappear between enumeration and statistics read.
                }
            }
        }
        catch
        {
            // Return the last known empty sample; the next tick retries automatically.
        }

        return counters;
    }

    private static bool LooksVirtual(NetworkInterface adapter)
    {
        var text = $"{adapter.Name} {adapter.Description}".ToLowerInvariant();
        var markers = new[]
        {
            "vmware", "virtualbox", "hyper-v", "hyperv", "wsl", "loopback",
            "teredo", "isatap", "pseudo", "docker", "container"
        };
        return markers.Any(text.Contains);
    }

    private static string? SelectAdapter(
        string adapterId,
        IReadOnlyDictionary<string, NetworkCounters> current,
        IReadOnlyDictionary<string, NetworkRate> rates)
    {
        if (!string.IsNullOrWhiteSpace(adapterId) && !adapterId.Equals("Auto", StringComparison.OrdinalIgnoreCase))
        {
            return current.ContainsKey(adapterId) && rates.ContainsKey(adapterId) ? adapterId : null;
        }

        var active = rates
            .OrderByDescending(pair => pair.Value.DownloadBytesPerSecond + pair.Value.UploadBytesPerSecond)
            .Select(pair => pair.Key)
            .FirstOrDefault();

        if (active is not null)
            return active;

        return current.OrderByDescending(pair => pair.Value.LinkSpeed).Select(pair => pair.Key).FirstOrDefault();
    }

    private static double AddSmoothed(Queue<double> samples, double value)
    {
        samples.Enqueue(Math.Max(0, value));
        while (samples.Count > 3)
            samples.Dequeue();
        return samples.Average();
    }

    private readonly record struct NetworkCounters(long BytesReceived, long BytesSent, long LinkSpeed);
    private readonly record struct NetworkRate(double DownloadBytesPerSecond, double UploadBytesPerSecond);

    public sealed record NetworkSample(
        double? DownloadBytesPerSecond,
        double? UploadBytesPerSecond,
        string? AdapterId)
    {
        public static NetworkSample Empty { get; } = new(null, null, null);
    }
}
