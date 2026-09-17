namespace WinFloat.Services;

public sealed class MemoryUsageReader
{
    public double? Read()
    {
        var status = new NativeMethods.MemoryStatusEx
        {
            Length = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MemoryStatusEx>()
        };

        if (!NativeMethods.GlobalMemoryStatusEx(ref status) || status.TotalPhys == 0)
            return null;

        return Math.Clamp((status.TotalPhys - status.AvailPhys) * 100d / status.TotalPhys, 0, 100);
    }
}
