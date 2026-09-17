using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using WinFloat.Models;

namespace WinFloat.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    static SettingsService()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public SettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        ConfigDirectory = Path.Combine(appData, "WinFloat");
        ConfigPath = Path.Combine(ConfigDirectory, "config.json");
        LogPath = Path.Combine(ConfigDirectory, "app.log");
    }

    public string ConfigDirectory { get; }
    public string ConfigPath { get; }
    public string LogPath { get; }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
                return new AppSettings();

            var json = File.ReadAllText(ConfigPath, Encoding.UTF8);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch (Exception exception)
        {
            LogError("读取配置失败", exception);
            return new AppSettings();
        }
    }

    public bool Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            var tempPath = ConfigPath + ".tmp";
            File.WriteAllText(tempPath, json, new UTF8Encoding(false));
            File.Move(tempPath, ConfigPath, true);
            return true;
        }
        catch (Exception exception)
        {
            LogError("保存配置失败", exception);
            return false;
        }
    }

    public void LogError(string message, Exception? exception = null)
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);
            if (File.Exists(LogPath) && new FileInfo(LogPath).Length > 5 * 1024 * 1024)
                File.Delete(LogPath);

            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            if (exception is not null)
                line += $" | {exception.GetType().Name}: {exception.Message}";
            File.AppendAllText(LogPath, line + Environment.NewLine, Encoding.UTF8);
        }
        catch
        {
            // Logging must never become a reason for the monitor to stop.
        }
    }
}
