using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GPUAssign.Models;
using Microsoft.Win32;

namespace GPUAssign.Services;

/// <summary>
/// Reads and writes Windows per-app GPU preferences stored at:
/// HKCU\Software\Microsoft\DirectX\UserGpuPreferences
/// </summary>
public static class GpuPreferenceService
{
    private const string RegKeyPath = @"Software\Microsoft\DirectX\UserGpuPreferences";

    private static readonly Regex VersionDirRegex =
        new(@"[\\/](\d+\.\d+\.\d+(?:\.\d+)?|[a-zA-Z]*-\d+\.\d+\.\d+)[\\/]", RegexOptions.Compiled);

    /// <summary>Read the GPU preference currently stored for the given EXE path or Store App ID.</summary>
    public static GpuPreference? GetPreference(string exePath)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegKeyPath, writable: false);
            if (key is null) return null;

            var raw = key.GetValue(exePath) as string;
            return ParsePreference(raw);
        }
        catch { return null; }
    }

    /// <summary>Write the GPU preference for the given EXE path or Store App ID.</summary>
    public static void SetPreference(string exePath, GpuPreference preference)
    {
        if (!Enum.IsDefined(preference)) throw new ArgumentOutOfRangeException(nameof(preference));
        using var key = Registry.CurrentUser.CreateSubKey(RegKeyPath, writable: true);
        // Windows stores Auto HDR / windowed optimizations in the same value.
        var fields = (key.GetValue(exePath) as string ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Where(field => !field.TrimStart().StartsWith("GpuPreference=", StringComparison.OrdinalIgnoreCase));
        var value = string.Join(';', fields.Append($"GpuPreference={(int)preference}")) + ";";
        key.SetValue(exePath, value, RegistryValueKind.String);
    }

    /// <summary>Remove the GPU preference entry for the given EXE path or Store App ID.</summary>
    public static void RemovePreference(string exePath)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegKeyPath, writable: true);
            key?.DeleteValue(exePath, throwOnMissingValue: false);
        }
        catch { /* ignore */ }
    }

    /// <summary>Return all entries currently registered in UserGpuPreferences.</summary>
    public static Dictionary<string, GpuPreference> GetAllPreferences()
    {
        var result = new Dictionary<string, GpuPreference>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegKeyPath, writable: false);
            if (key is null) return result;

            foreach (var name in key.GetValueNames())
            {
                // Skip global system settings
                if (string.Equals(name, "DirectXUserGlobalSettings", StringComparison.OrdinalIgnoreCase))
                    continue;

                var raw = key.GetValue(name) as string;
                var pref = ParsePreference(raw);
                if (pref.HasValue)
                    result[name] = pref.Value;
            }
        }
        catch { /* ignore */ }
        return result;
    }

    /// <summary>
    /// Scans all existing GPU preferences set in Windows Settings and converts them
    /// into smart AppDefinition objects ready for import.
    /// Recognizes both regular Win32 EXEs and Microsoft Store (packaged) apps.
    /// </summary>
    public static List<AppDefinition> ScanExistingWindowsPreferences()
    {
        var existing = GetAllPreferences();
        var list = new List<AppDefinition>();

        foreach (var (keyName, gpuPref) in existing)
        {
            try
            {
                // 1. Check if this is a Microsoft Store (UWP/AUMID) package entry
                bool isStoreApp = !keyName.Contains('\\') && !keyName.Contains('/') &&
                                  (keyName.Contains('!') || keyName.Contains('_'));

                if (isStoreApp)
                {
                    // Get official localized display name from package metadata
                    string appName = StoreAppService.GetStoreAppDisplayName(keyName);

                    var storeAppDef = new AppDefinition
                    {
                        Name           = appName,
                        Category       = L.Get("category.storeApp"),
                        SearchPath     = L.Get("field.storeAppNoPath"),
                        ExeName        = keyName,
                        SearchMode     = SearchMode.StoreApp,
                        Recursive      = false,
                        GpuPreference  = gpuPref,
                        CurrentExePath = keyName,
                        ManagedPaths   = new List<string> { keyName }
                    };

                    list.Add(storeAppDef);
                    continue;
                }

                // 2. Standard Win32 EXE path
                var exeName = Path.GetFileName(keyName);
                var dirName = Path.GetDirectoryName(keyName) ?? string.Empty;

                // Determine display name (check FileVersionInfo, then StoreAppService if under WindowsApps)
                string win32AppName = Path.GetFileNameWithoutExtension(keyName);
                if (keyName.Contains("WindowsApps", StringComparison.OrdinalIgnoreCase))
                {
                    var storeName = StoreAppService.GetStoreAppDisplayName(keyName);
                    if (!string.IsNullOrWhiteSpace(storeName) && storeName != win32AppName)
                        win32AppName = storeName;
                }

                if (File.Exists(keyName))
                {
                    try
                    {
                        var info = FileVersionInfo.GetVersionInfo(keyName);
                        if (!string.IsNullOrWhiteSpace(info.FileDescription))
                            win32AppName = info.FileDescription;
                        else if (!string.IsNullOrWhiteSpace(info.ProductName))
                            win32AppName = info.ProductName;
                    }
                    catch { }
                }

                // Analyze versioned folder structure (e.g. LINE\bin\26.4.0.3944\LINE.exe or Discord\app-1.0.9253\Discord.exe)
                var versionMatch = VersionDirRegex.Match(keyName);
                SearchMode mode = SearchMode.Fixed;
                string searchPath = dirName;
                bool recursive = false;

                if (versionMatch.Success)
                {
                    var versionSegment = versionMatch.Groups[1].Value;
                    if (versionSegment.StartsWith("app-", StringComparison.OrdinalIgnoreCase))
                    {
                        // Discord / Squirrel style (app-1.0.9xxx) -> Glob pattern
                        var parentDir = keyName.Substring(0, versionMatch.Index);
                        searchPath = Path.Combine(parentDir, "app-*");
                        mode = SearchMode.Glob;
                        recursive = false;
                    }
                    else
                    {
                        // SemVer folder style (LINE etc.) -> LatestVersion recursive
                        var parentDir = keyName.Substring(0, versionMatch.Index);
                        searchPath = parentDir;
                        mode = SearchMode.LatestVersion;
                        recursive = true;
                    }
                }

                // Compress search path to environment variables if applicable
                searchPath = CompressToEnvVars(searchPath);

                var appDef = new AppDefinition
                {
                    Name           = win32AppName,
                    Category       = L.Get("category.imported"),
                    SearchPath     = searchPath,
                    ExeName        = exeName,
                    SearchMode     = mode,
                    Recursive      = recursive,
                    GpuPreference  = gpuPref,
                    CurrentExePath = keyName,
                    ManagedPaths   = new List<string> { keyName }
                };

                list.Add(appDef);
            }
            catch { }
        }

        return list;
    }

    /// <summary>Format a user-friendly name from an AUMID string.</summary>
    private static string FormatStoreAppName(string aumid)
    {
        // Examples:
        // Microsoft.Windows.Photos_8wekyb3d8bbwe!App -> Microsoft.Windows.Photos
        // OpenAI.ChatGPT-Desktop_2p2nqsd0c76g0!ChatGPT -> ChatGPT (Desktop)
        // AppleInc.AppleMusicWin_nzyj5cx40ttqa!App -> AppleMusicWin
        try
        {
            var raw = aumid;
            var bangIdx = raw.IndexOf('!');
            var appPart = bangIdx >= 0 ? raw[(bangIdx + 1)..] : string.Empty;
            var pfnPart = bangIdx >= 0 ? raw[..bangIdx] : raw;

            var underscoreIdx = pfnPart.IndexOf('_');
            var baseName = underscoreIdx >= 0 ? pfnPart[..underscoreIdx] : pfnPart;

            if (baseName.StartsWith("Microsoft.Windows.", StringComparison.OrdinalIgnoreCase))
                return baseName["Microsoft.Windows.".Length..];

            if (baseName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase))
                return baseName["Microsoft.".Length..];

            if (baseName.StartsWith("AppleInc.", StringComparison.OrdinalIgnoreCase))
                return baseName["AppleInc.".Length..];

            if (baseName.StartsWith("OpenAI.", StringComparison.OrdinalIgnoreCase))
                return baseName["OpenAI.".Length..];

            return baseName;
        }
        catch
        {
            return aumid;
        }
    }

    /// <summary>Compress absolute path segments into standard Windows environment variables.</summary>
    private static string CompressToEnvVars(string path)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appData      = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var progFiles    = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        if (!string.IsNullOrEmpty(localAppData) && path.StartsWith(localAppData, StringComparison.OrdinalIgnoreCase))
            return "%LOCALAPPDATA%" + path.Substring(localAppData.Length);

        if (!string.IsNullOrEmpty(appData) && path.StartsWith(appData, StringComparison.OrdinalIgnoreCase))
            return "%APPDATA%" + path.Substring(appData.Length);

        if (!string.IsNullOrEmpty(progFiles) && (path.Equals(progFiles, StringComparison.OrdinalIgnoreCase) || path.StartsWith(progFiles + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
            return "%PROGRAMFILES%" + path.Substring(progFiles.Length);

        if (!string.IsNullOrEmpty(progFilesX86) && path.StartsWith(progFilesX86, StringComparison.OrdinalIgnoreCase))
            return "%PROGRAMFILES(X86)%" + path.Substring(progFilesX86.Length);

        return path;
    }

    /// <summary>Export all current preferences as a .reg file string for backup.</summary>
    public static string ExportToReg()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Windows Registry Editor Version 5.00");
        sb.AppendLine();
        sb.AppendLine(@$"[HKEY_CURRENT_USER\{RegKeyPath}]");

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegKeyPath, writable: false);
            if (key is not null)
            {
                foreach (var name in key.GetValueNames())
                {
                    var raw = key.GetValue(name) as string;
                    var escapedName = name.Replace("\\", "\\\\").Replace("\"", "\\\"");
                    var escapedVal  = EscapeRegString(raw ?? string.Empty);
                    sb.AppendLine($"\"{escapedName}\"=\"{escapedVal}\"");
                }
            }
        }
        catch { /* ignore */ }

        return sb.ToString();
    }

    /// <summary>Import (restore) a .reg export string, overwriting affected values.</summary>
    public static void ImportFromReg(string regContent)
    {
        // Validate all values before changing the registry, and accept only this key.
        var values = new List<(string Name, string Value)>();
        bool inGpuKey = false;
        foreach (var line in regContent.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('['))
            {
                inGpuKey = trimmed.Equals($"[HKEY_CURRENT_USER\\{RegKeyPath}]", StringComparison.OrdinalIgnoreCase);
                continue;
            }
            if (!inGpuKey) continue;
            if (!trimmed.StartsWith('"')) continue;

            var eqIdx = trimmed.IndexOf("\"=\"", StringComparison.Ordinal);
            if (eqIdx < 0) continue;

            if (!trimmed.EndsWith('"')) continue;
            var name = UnescapeRegString(trimmed[1..eqIdx]);
            var value = UnescapeRegString(trimmed[(eqIdx + 3)..^1]);

            if (name.Contains('\n') || name.Contains('\r') || value.Contains('\n') || value.Contains('\r'))
                throw new InvalidDataException("Invalid registry backup value.");
            values.Add((name, value));
        }
        if (values.Count == 0) throw new InvalidDataException("The backup contains no GPU preference values.");
        using var key = Registry.CurrentUser.CreateSubKey(RegKeyPath, writable: true);
        foreach (var (name, value) in values) key.SetValue(name, value, RegistryValueKind.String);
    }

    // ---- helpers ----

    private static GpuPreference? ParsePreference(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return null;
        var match = Regex.Match(raw, @"(?:^|;)\s*GpuPreference=([0-2])(?:;|$)", RegexOptions.IgnoreCase);
        if (!match.Success) return null;
        return (GpuPreference)int.Parse(match.Groups[1].Value);
    }

    private static string EscapeRegString(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string UnescapeRegString(string value)
    {
        var result = new System.Text.StringBuilder();
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length && (value[i + 1] == '\\' || value[i + 1] == '"')) i++;
            result.Append(value[i]);
        }
        return result.ToString();
    }
}
