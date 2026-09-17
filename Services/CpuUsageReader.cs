namespace WinFloat.Services;

public sealed class CpuUsageReader
{
    private ulong _lastIdle;
    private ulong _lastKernel;
    private ulong _lastUser;
    private bool _hasSample;

    public double? Read()
    {
        if (!NativeMethods.GetSystemTimes(out var idle, out var kernel, out var user))
            return null;

        var idleValue = ToUInt64(idle);
        var kernelValue = ToUInt64(kernel);
        var userValue = ToUInt64(user);

        if (!_hasSample)
        {
            _lastIdle = idleValue;
            _lastKernel = kernelValue;
            _lastUser = userValue;
            _hasSample = true;
            return null;
        }

        var idleDelta = idleValue - _lastIdle;
        var totalDelta = (kernelValue - _lastKernel) + (userValue - _lastUser);

        _lastIdle = idleValue;
        _lastKernel = kernelValue;
        _lastUser = userValue;

        if (totalDelta == 0)
            return null;

        var busyDelta = totalDelta > idleDelta ? totalDelta - idleDelta : 0;
        return Math.Clamp(busyDelta * 100d / totalDelta, 0, 100);
    }

    private static ulong ToUInt64(NativeMethods.FileTime fileTime)
    {
        return ((ulong)fileTime.HighDateTime << 32) | fileTime.LowDateTime;
    }
}
