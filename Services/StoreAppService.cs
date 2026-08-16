using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Management.Deployment;
using Windows.Storage.Streams;

namespace GPUAssign.Services;

/// <summary>
/// Service to query official metadata and icons for Microsoft Store (UWP / MSIX / WindowsApps) packaged applications.
/// </summary>
public static class StoreAppService
{
    private static readonly ConcurrentDictionary<string, string> NameCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Regex WindowsAppsRegex = new(@"[\\/]WindowsApps[\\/]([^\\/]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Checks whether the given key or path belongs to a Microsoft Store / WindowsApps package.
    /// </summary>
    public static bool IsStoreAppId(string keyOrPath)
    {
        if (string.IsNullOrEmpty(keyOrPath)) return false;

        // 1. Pure AUMID (e.g. Microsoft.Windows.Photos_8wekyb3d8bbwe!App)
        if (!keyOrPath.Contains('\\') && !keyOrPath.Contains('/') && (keyOrPath.Contains('!') || keyOrPath.Contains('_')))
            return true;

        // 2. WindowsApps folder path (e.g. C:\Program Files\WindowsApps\AppleInc.iCloud_...)
        if (keyOrPath.Contains("WindowsApps", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>
    /// Gets the official localized application display name from Windows Store package metadata.
    /// </summary>
    public static string GetStoreAppDisplayName(string keyOrPath)
    {
        if (string.IsNullOrEmpty(keyOrPath)) return keyOrPath;

        if (NameCache.TryGetValue(keyOrPath, out var cached) && !string.IsNullOrEmpty(cached))
            return cached;

        try
        {
            var pfn = ExtractPackageFamilyName(keyOrPath);
            if (!string.IsNullOrEmpty(pfn))
            {
                var packageManager = new PackageManager();
                var packages = packageManager.FindPackagesForUser("", pfn);

                foreach (var pkg in packages)
                {
                    try
                    {
                        var appEntries = pkg.GetAppListEntriesAsync().AsTask().GetAwaiter().GetResult();
                        if (appEntries != null && appEntries.Count > 0)
                        {
                            // If AUMID match
                            foreach (var entry in appEntries)
                            {
                                if (string.Equals(entry.AppUserModelId, keyOrPath, StringComparison.OrdinalIgnoreCase))
                                {
                                    var name = entry.DisplayInfo.DisplayName;
                                    if (!string.IsNullOrWhiteSpace(name))
                                    {
                                        NameCache[keyOrPath] = name;
                                        return name;
                                    }
                                }
                            }

                            // Otherwise first entry display name
                            var first = appEntries[0].DisplayInfo.DisplayName;
                            if (!string.IsNullOrWhiteSpace(first))
                            {
                                NameCache[keyOrPath] = first;
                                return first;
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(pkg.DisplayName))
                        {
                            NameCache[keyOrPath] = pkg.DisplayName;
                            return pkg.DisplayName;
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }

        var fallback = FallbackStoreAppName(keyOrPath);
        NameCache[keyOrPath] = fallback;
        return fallback;
    }

    /// <summary>
    /// Loads the official Microsoft Store application icon as a WinUI 3 BitmapImage asynchronously.
    /// </summary>
    public static async Task<BitmapImage?> GetStoreAppIconAsync(string keyOrPath)
    {
        if (string.IsNullOrEmpty(keyOrPath)) return null;

        try
        {
            var pfn = ExtractPackageFamilyName(keyOrPath);
            if (!string.IsNullOrEmpty(pfn))
            {
                var packageManager = new PackageManager();
                var packages = packageManager.FindPackagesForUser("", pfn);

                foreach (var pkg in packages)
                {
                    try
                    {
                        var appEntries = await pkg.GetAppListEntriesAsync();
                        if (appEntries != null && appEntries.Count > 0)
                        {
                            foreach (var entry in appEntries)
                            {
                                if (string.Equals(entry.AppUserModelId, keyOrPath, StringComparison.OrdinalIgnoreCase) || !keyOrPath.Contains('!'))
                                {
                                    var logoRef = entry.DisplayInfo.GetLogo(new Size(48, 48));
                                    if (logoRef != null)
                                    {
                                        using var stream = await logoRef.OpenReadAsync();
                                        var bitmap = new BitmapImage();
                                        await bitmap.SetSourceAsync(stream);
                                        return bitmap;
                                    }
                                }
                            }
                        }
                    }
                    catch { }

                    try
                    {
                        if (pkg.Logo != null)
                        {
                            var bitmap = new BitmapImage(pkg.Logo);
                            return bitmap;
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }

        return null;
    }

    /// <summary>
    /// Extracts the package family name or package full name prefix from an AUMID or WindowsApps path.
    /// </summary>
    private static string? ExtractPackageFamilyName(string keyOrPath)
    {
        // 1. WindowsApps path: C:\Program Files\WindowsApps\AppleInc.iCloud_15.8.127.0_x64__nzyj5cx40ttqa\iCloud.exe
        var match = WindowsAppsRegex.Match(keyOrPath);
        if (match.Success)
        {
            var folder = match.Groups[1].Value;
            var parts = folder.Split('_');
            if (parts.Length >= 5)
            {
                // Reconstruct PackageFamilyName: {Name}_{PublisherId}
                return $"{parts[0]}_{parts[^1]}";
            }
            return parts[0];
        }

        // 2. Pure AUMID: Microsoft.Windows.Photos_8wekyb3d8bbwe!App
        var bangIdx = keyOrPath.IndexOf('!');
        return bangIdx >= 0 ? keyOrPath[..bangIdx] : keyOrPath;
    }

    /// <summary>
    /// Extracts a readable fallback name from AUMID or path when package metadata is unavailable.
    /// </summary>
    public static string FallbackStoreAppName(string keyOrPath)
    {
        try
        {
            if (keyOrPath.Contains('\\') || keyOrPath.Contains('/'))
            {
                return Path.GetFileNameWithoutExtension(keyOrPath);
            }

            var raw = keyOrPath;
            var bangIdx = raw.IndexOf('!');
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
            return keyOrPath;
        }
    }
}
