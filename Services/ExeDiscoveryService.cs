using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GPUAssign.Models;

namespace GPUAssign.Services;

/// <summary>
/// Discovers the best-match EXE for an AppDefinition.
///
/// Supported search modes:
///   Fixed         – verify existence of searchPath\exe
///   LatestVersion – recursive/non-recursive scan, pick by version / file version / mtime
///   Glob          – wildcard (*?) in searchPath dir segments, then find exe
///   Regex         – regex match on full paths under searchPath
/// </summary>
public static class ExeDiscoveryService
{
    private static readonly Regex VersionFolderPattern =
        new(@"(\d+)\.(\d+)\.(\d+)(?:\.(\d+))?", RegexOptions.Compiled);

    // ──────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────

    /// <summary>Expand environment variables in a path.</summary>
    public static string ExpandPath(string path) =>
        Environment.ExpandEnvironmentVariables(path);

    /// <summary>Find all candidate EXE paths for the app definition.</summary>
    public static List<string> FindAllMatches(AppDefinition app)
    {
        return app.SearchMode switch
        {
            SearchMode.Fixed         => FindFixed(app),
            SearchMode.LatestVersion => FindLatestVersion(app),
            SearchMode.Glob          => FindGlob(app),
            SearchMode.Regex         => FindRegex(app),
            SearchMode.StoreApp      => string.IsNullOrEmpty(app.ExeName) ? new() : new() { app.ExeName },
            _                        => FindLatestVersion(app)
        };
    }

    /// <summary>Return the single best EXE path, or null if nothing found.</summary>
    public static string? FindBestMatch(AppDefinition app)
    {
        var candidates = FindAllMatches(app);
        if (candidates.Count == 0) return null;
        if (candidates.Count == 1) return candidates[0];

        return candidates
            .OrderByDescending(GetVersionScore)
            .ThenByDescending(GetFileVersion)
            .ThenByDescending(File.GetLastWriteTime)
            .First();
    }

    // ──────────────────────────────────────────────
    // Mode implementations
    // ──────────────────────────────────────────────

    private static List<string> FindFixed(AppDefinition app)
    {
        var expanded = ExpandPath(app.SearchPath);

        // If the user supplied a full path ending in .exe, treat it as-is
        if (expanded.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(expanded))
            return new List<string> { expanded };

        // Otherwise combine searchPath + exe
        var full = Path.Combine(expanded, app.ExeName);
        return File.Exists(full) ? new List<string> { full } : new List<string>();
    }

    private static List<string> FindLatestVersion(AppDefinition app)
    {
        var basePath = ExpandPath(app.SearchPath);
        if (!Directory.Exists(basePath)) return new List<string>();

        var option = app.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        try
        {
            return Directory.EnumerateFiles(basePath, app.ExeName, option).ToList();
        }
        catch (UnauthorizedAccessException)
        {
            return SafeEnumerate(basePath, app.ExeName, app.Recursive);
        }
    }

    private static List<string> FindGlob(AppDefinition app)
    {
        // Expand env vars first
        var pattern = ExpandPath(app.SearchPath);

        // Normalise separators
        pattern = pattern.Replace('/', Path.DirectorySeparatorChar);

        // Split into segments and find first wildcard
        var parts = pattern.Split(Path.DirectorySeparatorChar);

        var results = new List<string>();
        GlobSearch(results, string.Empty, parts, 0, app.ExeName);
        return results;
    }

    private static void GlobSearch(List<string> results, string current,
                                   string[] segments, int idx, string exeName)
    {
        // If we've consumed all segments, look for the EXE here
        if (idx >= segments.Length)
        {
            if (!string.IsNullOrEmpty(current) && Directory.Exists(current))
                SafeAddFiles(results, current, exeName);
            return;
        }

        var seg = segments[idx];
        bool isRoot = idx == 0;

        // No wildcard in this segment → advance normally
        if (!seg.Contains('*') && !seg.Contains('?'))
        {
            var next = isRoot ? seg : Path.Combine(current, seg);
            // For the very first segment on Windows (e.g. "C:"), just pass it through
            if (isRoot || Directory.Exists(next))
                GlobSearch(results, next, segments, idx + 1, exeName);
            return;
        }

        // "**" means recursive: match zero or more directory levels
        if (seg == "**")
        {
            // Match at current level (zero levels consumed)
            GlobSearch(results, current, segments, idx + 1, exeName);
            // Match inside each subdirectory
            try
            {
                foreach (var sub in Directory.EnumerateDirectories(current))
                    GlobSearch(results, sub, segments, idx, exeName);   // stay on **
            }
            catch { }
            return;
        }

        // Regular wildcard (* ?): enumerate matching subdirectories
        if (string.IsNullOrEmpty(current)) return;
        try
        {
            foreach (var sub in Directory.EnumerateDirectories(current, seg))
                GlobSearch(results, sub, segments, idx + 1, exeName);
        }
        catch { }
    }

    private static void SafeAddFiles(List<string> results, string dir, string pattern)
    {
        // pattern may also be a glob (e.g. "java*.exe")
        try
        {
            results.AddRange(Directory.EnumerateFiles(dir, pattern));
        }
        catch { }
    }

    private static List<string> FindRegex(AppDefinition app)
    {
        var basePath = ExpandPath(app.SearchPath);
        if (!Directory.Exists(basePath)) return new List<string>();

        Regex rx;
        try { rx = new Regex(app.ExeName, RegexOptions.IgnoreCase | RegexOptions.Compiled); }
        catch { return new List<string>(); }

        var allFiles = SafeEnumerate(basePath, "*", recursive: true);
        return allFiles.Where(f =>
        {
            var relative = f.Substring(Math.Min(basePath.Length + 1, f.Length));
            return rx.IsMatch(relative);
        }).ToList();
    }

    // ──────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────

    private static Version GetVersionScore(string exePath)
    {
        var parts = exePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (var part in parts.Reverse())
        {
            var m = VersionFolderPattern.Match(part);
            if (!m.Success) continue;
            try
            {
                int major = int.Parse(m.Groups[1].Value);
                int minor = int.Parse(m.Groups[2].Value);
                int build = int.Parse(m.Groups[3].Value);
                int rev   = m.Groups[4].Success ? int.Parse(m.Groups[4].Value) : 0;
                return new Version(major, minor, build, rev);
            }
            catch { }
        }
        return new Version(0, 0, 0, 0);
    }

    private static Version GetFileVersion(string exePath)
    {
        try
        {
            var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(exePath);
            return new Version(info.FileMajorPart, info.FileMinorPart,
                               info.FileBuildPart, info.FilePrivatePart);
        }
        catch { return new Version(0, 0, 0, 0); }
    }

    private static List<string> SafeEnumerate(string root, string pattern, bool recursive)
    {
        var result = new List<string>();
        var queue  = new Queue<string>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var dir = queue.Dequeue();
            try
            {
                result.AddRange(Directory.EnumerateFiles(dir, pattern));
                if (!recursive) break;
                foreach (var sub in Directory.EnumerateDirectories(dir))
                    queue.Enqueue(sub);
            }
            catch (UnauthorizedAccessException) { }
        }
        return result;
    }
}
