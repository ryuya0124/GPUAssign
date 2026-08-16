using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                var json = File.ReadAllText(ConfigFilePath);
                return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
            }
        }
        catch { /* ignore */ }

        return new AppConfig();
    }

    public static void Save(AppConfig config)
    {
        try
        {
            File.WriteAllText(ConfigFilePath, JsonSerializer.Serialize(config, JsonOptions));
        }
        catch { /* best effort */ }
    }

    // ── Catalog (default_apps.json, optional presets) ──

    public static List<AppDefinition> LoadCatalog()
    {
        try
        {
            var catalogPath = Path.Combine(ConfigDir, "Assets", "default_apps.json");
            if (!File.Exists(catalogPath)) return new();

            var json = File.ReadAllText(catalogPath);
            var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
            return config?.Apps ?? new();
        }
        catch { return new(); }
    }

    public static List<AppDefinition> GetUnadoptedCatalogEntries(AppConfig userConfig)
    {
        var catalog = LoadCatalog();
        var userKeys = userConfig.Apps
            .Select(a => (a.Name, a.ExeName))
            .ToHashSet();

        return catalog
            .Where(c => !userKeys.Contains((c.Name, c.ExeName)))
            .ToList();
    }
}
