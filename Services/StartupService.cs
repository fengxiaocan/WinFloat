using Microsoft.Win32;
using System.IO;

namespace WinFloat.Services;

public sealed class StartupService
{
    private const string RunKeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    private const string ValueName = "WinFloat";

    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
        }
        catch
        {
            return false;
        }
    }

    public bool SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (key is null)
                return false;

            if (!enabled)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                return true;
            }

            key.SetValue(ValueName, BuildCommandLine(), RegistryValueKind.String);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string BuildCommandLine()
    {
        var processPath = Environment.ProcessPath ?? string.Empty;
        var entryName = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name;

        if (!string.IsNullOrWhiteSpace(entryName) &&
            Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            var managedEntry = Path.Combine(AppContext.BaseDirectory, entryName + ".dll");
            return $"\"{processPath}\" \"{managedEntry}\"";
        }

        return $"\"{processPath}\"";
    }
}
