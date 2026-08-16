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

    /// <summary>Read the GPU preference currently stored for the given EXE path.</summary>
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

    /// <summary>Write the GPU preference for the given EXE path.</summary>
    public static void SetPreference(string exePath, GpuPreference preference)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RegKeyPath, writable: true);
        var value = FormatPreference(preference);
        key.SetValue(exePath, value, RegistryValueKind.String);
    }

    /// <summary>Remove the GPU preference entry for the given EXE path.</summary>
    public static void RemovePreference(string exePath)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegKeyPath, writable: true);
            key?.DeleteValue(exePath, throwOnMissingValue: false);
        }
        catch { /* ignore */ }
    }

    /// <summary>Return all EXE paths currently registered in UserGpuPreferences.</summary>
    public static Dictionary<string, GpuPreference> GetAllPreferences()
    {
        var result = new Dictionary<string, GpuPreference>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegKeyPath, writable: false);
            if (key is null) return result;

            foreach (var name in key.GetValueNames())
            {
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
    /// </summary>
    public static List<AppDefinition> ScanExistingWindowsPreferences()
    {
        var existing = GetAllPreferences();
        var list = new List<AppDefinition>();

        foreach (var (fullExePath, gpuPref) in existing)
        {
            try
            {
                var exeName = Path.GetFileName(fullExePath);
                var dirName = Path.GetDirectoryName(fullExePath) ?? string.Empty;

                // Determine display name
                string appName = Path.GetFileNameWithoutExtension(fullExePath);
                if (File.Exists(fullExePath))
                {
                    try
                    {
                        var info = FileVersionInfo.GetVersionInfo(fullExePath);
                        if (!string.IsNullOrWhiteSpace(info.FileDescription))
                            appName = info.FileDescription;
                        else if (!string.IsNullOrWhiteSpace(info.ProductName))
                            appName = info.ProductName;
                    }
                    catch { }
                }

                // Analyze versioned structure (e.g. LINE\bin\26.4.0.3944\LINE.exe or Discord\app-1.0.9253\Discord.exe)
                var versionMatch = VersionDirRegex.Match(fullExePath);
                SearchMode mode = SearchMode.Fixed;
                string searchPath = dirName;
                bool recursive = false;

                if (versionMatch.Success)
                {
                    var versionSegment = versionMatch.Groups[1].Value;
                    if (versionSegment.StartsWith("app-", StringComparison.OrdinalIgnoreCase))
                    {
                        // Discord / Squirrel style (app-1.0.9xxx) -> Glob pattern
                        var parentDir = fullExePath.Substring(0, versionMatch.Index);
                        searchPath = Path.Combine(parentDir, "app-*");
                        mode = SearchMode.Glob;
                        recursive = false;
                    }
                    else
                    {
                        // SemVer folder style (LINE etc.) -> LatestVersion recursive
                        var parentDir = fullExePath.Substring(0, versionMatch.Index);
                        searchPath = parentDir;
                        mode = SearchMode.LatestVersion;
                        recursive = true;
                    }
                }

                // Compress search path to environment variables if applicable
                searchPath = CompressToEnvVars(searchPath);

                var appDef = new AppDefinition
                {
                    Name          = appName,
                    Category      = "Windows設定からインポート",
                    SearchPath    = searchPath,
                    ExeName       = exeName,
                    SearchMode    = mode,
                    Recursive     = recursive,
                    GpuPreference = gpuPref,
                    CurrentExePath = fullExePath,
                    ManagedPaths  = new List<string> { fullExePath }
                };

                list.Add(appDef);
            }
            catch { }
        }

        return list;
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

        if (!string.IsNullOrEmpty(progFiles) && path.StartsWith(progFiles, StringComparison.OrdinalIgnoreCase))
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
                    var escapedVal  = (raw ?? string.Empty).Replace("\\", "\\\\");
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
        using var key = Registry.CurrentUser.CreateSubKey(RegKeyPath, writable: true);
        foreach (var line in regContent.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith('"')) continue;

            var eqIdx = trimmed.IndexOf("\"=\"", StringComparison.Ordinal);
            if (eqIdx < 0) continue;

            var name  = trimmed[1..eqIdx].Replace("\\\\", "\\").Replace("\\\"", "\"");
            var value = trimmed[(eqIdx + 3)..].TrimEnd('"', '\r').Replace("\\\\", "\\");

            key.SetValue(name, value, RegistryValueKind.String);
        }
    }

    // ---- helpers ----

    private static GpuPreference? ParsePreference(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return null;
        var match = Regex.Match(raw, @"GpuPreference=(\d)");
        if (!match.Success) return null;
        return (GpuPreference)int.Parse(match.Groups[1].Value);
    }

    private static string FormatPreference(GpuPreference pref)
        => $"GpuPreference={(int)pref};";
}
