using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using GPUAssign.Models;

namespace GPUAssign.Services;

/// <summary>
/// Manages persistence of user settings and data in a 100% portable manner.
/// All files (apps.json, sync_log.json, backups/) are stored in the same folder
/// as the executing GPUAssign.exe, completely independent of AppData.
/// </summary>
public static class ConfigService
{
    private static readonly object Gate = new();
    private static AppConfig? _current;
    /// <summary>
    /// Base directory of the running executable.
    /// Everything is stored here for complete portability.
    /// </summary>
    public static string ConfigDir => AppContext.BaseDirectory;

    public static string ConfigFilePath => Path.Combine(ConfigDir, "apps.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters    = { new JsonStringEnumConverter() }
    };

    // ── User config (stored beside GPUAssign.exe) ──

    public static AppConfig Load()
    {
        lock (Gate)
        {
            if (_current != null) return _current;
            if (File.Exists(ConfigFilePath))
            {
                var json = File.ReadAllText(ConfigFilePath);
                var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions)
                    ?? throw new InvalidDataException("apps.json must contain a configuration object.");
                if (config.Apps == null || config.Apps.Any(app => app == null))
                    throw new InvalidDataException("apps.json contains an invalid app list.");
                foreach (var app in config.Apps) app.ManagedPaths ??= new();
                return _current = config;
            }
            return _current = new AppConfig();
        }
    }

    public static void Save(AppConfig config)
    {
        lock (Gate)
        {
            string temp = ConfigFilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temp, JsonSerializer.Serialize(config, JsonOptions));
                File.Move(temp, ConfigFilePath, overwrite: true);
                _current = config;
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
