using System;
using System.Collections.Generic;
using System.Linq;
using GPUAssign.Models;
using Microsoft.Win32;

namespace GPUAssign.Services;

/// <summary>
/// Reads and writes Windows per-app GPU preferences stored at:
/// HKCU\Software\Microsoft\DirectX\UserGpuPreferences
///
/// Each value is:
///   name  = full path to the EXE
///   value = "GpuPreference=N;" where N is 0 (default), 1 (power-saving), 2 (high-performance)
/// </summary>
public static class GpuPreferenceService
{
    private const string RegKeyPath = @"Software\Microsoft\DirectX\UserGpuPreferences";

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
                    // Escape backslashes and quotes for .reg format
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
        // Simple parser – only handles the format we write in ExportToReg
        using var key = Registry.CurrentUser.CreateSubKey(RegKeyPath, writable: true);
        foreach (var line in regContent.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith('"')) continue;

            // "name"="value"
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
        // Format: "GpuPreference=2;" or sometimes just "GpuPreference=2"
        var match = System.Text.RegularExpressions.Regex.Match(raw, @"GpuPreference=(\d)");
        if (!match.Success) return null;
        return (GpuPreference)int.Parse(match.Groups[1].Value);
    }

    private static string FormatPreference(GpuPreference pref)
        => $"GpuPreference={(int)pref};";
}
