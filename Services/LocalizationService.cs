using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace GPUAssign.Services;

/// <summary>
/// JSON-based localization service.
/// Detects the OS UI language on first use and loads the matching locale file
/// from the app's output directory (Assets/Locales/).
/// Falls back to ja-JP if the detected language has no matching file.
/// </summary>
public sealed class LocalizationService
{
    private static LocalizationService? _instance;

    /// <summary>Singleton instance – initialized from the saved language preference.</summary>
    public static LocalizationService Instance => _instance ??= new LocalizationService();

    private Dictionary<string, string> _strings = new();

    /// <summary>Currently active locale identifier, e.g. "ja-JP".</summary>
    public string CurrentLocale { get; private set; } = "ja-JP";

    /// <summary>Available locales discovered in the Locales directory.</summary>
    public IReadOnlyList<string> AvailableLocales { get; private set; } = new[] { "ja-JP", "en-US" };

    private LocalizationService() { }

    /// <summary>
    /// Initialize with a specific locale.
    /// Call once on app startup before any UI is built.
    /// </summary>
    public static void Initialize(string? preferredLocale = null)
    {
        _instance = new LocalizationService();
        _instance.LoadLocale(preferredLocale);
    }

    private void LoadLocale(string? preferred)
    {
        // 1. Use explicit preference, 2. fall back to OS culture, 3. fall back to ja-JP
        var locale = preferred is not null and not "auto"
            ? preferred
            : MapCultureToLocale(CultureInfo.CurrentUICulture);

        var basePath = Path.Combine(AppContext.BaseDirectory, "Assets", "Locales");
        var filePath = Path.Combine(basePath, $"{locale}.json");

        if (!File.Exists(filePath))
        {
            // Try just the language part (e.g. "en" → "en-US")
            locale = locale.Length >= 2 ? $"{locale[..2]}-{locale[..2].ToUpperInvariant()}" : "ja-JP";
            filePath = Path.Combine(basePath, $"{locale}.json");
        }

        if (!File.Exists(filePath))
        {
            locale = "ja-JP";
            filePath = Path.Combine(basePath, "ja-JP.json");
        }

        try
        {
            if (File.Exists(filePath))
            {
                _strings = JsonSerializer.Deserialize<Dictionary<string, string>>(
                    File.ReadAllText(filePath)) ?? new();
                CurrentLocale = locale;
            }
        }
        catch { /* silently keep empty dict */ }

        // Discover available locales
        try
        {
            var files = Directory.GetFiles(basePath, "*.json");
            var locales = new List<string>();
            foreach (var f in files)
                locales.Add(Path.GetFileNameWithoutExtension(f));
            AvailableLocales = locales;
        }
        catch { }
    }

    private static string MapCultureToLocale(CultureInfo culture)
    {
        return culture.IetfLanguageTag switch
        {
            var t when t.StartsWith("ja") => "ja-JP",
            var t when t.StartsWith("en") => "en-US",
            _ => "ja-JP"
        };
    }

    /// <summary>Look up a localized string. Returns the key itself if not found.</summary>
    public string Get(string key) =>
        _strings.TryGetValue(key, out var val) ? val : key;

    /// <summary>Format a localized string with positional arguments ({0}, {1}, …).</summary>
    public string GetFormat(string key, params object[] args)
    {
        var template = Get(key);
        try { return string.Format(template, args); }
        catch { return template; }
    }
}

/// <summary>
/// Static shortcut so any file can write  L.Get("key")  without dependency injection.
/// </summary>
public static class L
{
    public static string Get(string key) => LocalizationService.Instance.Get(key);
    public static string F(string key, params object[] args) => LocalizationService.Instance.GetFormat(key, args);
}
