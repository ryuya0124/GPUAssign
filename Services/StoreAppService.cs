using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Management.Deployment;
using Windows.Storage.Streams;

namespace GPUAssign.Services;

/// <summary>
/// Service to query official metadata and icons for Microsoft Store (UWP / MSIX) packaged applications.
/// </summary>
public static class StoreAppService
{
    private static readonly ConcurrentDictionary<string, (string DisplayName, string? LogoPath)> MetadataCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Checks whether the given key is a Microsoft Store (UWP/AUMID) package entry.
    /// </summary>
    public static bool IsStoreAppId(string keyName)
    {
        if (string.IsNullOrEmpty(keyName)) return false;
        return !keyName.Contains('\\') && !keyName.Contains('/') &&
               (keyName.Contains('!') || keyName.Contains('_'));
    }

    /// <summary>
    /// Gets the official localized application display name from Windows Store package metadata.
    /// </summary>
    public static string GetStoreAppDisplayName(string aumid)
    {
        if (string.IsNullOrEmpty(aumid)) return aumid;

        if (MetadataCache.TryGetValue(aumid, out var cached) && !string.IsNullOrEmpty(cached.DisplayName))
            return cached.DisplayName;

        try
        {
            var bangIdx = aumid.IndexOf('!');
            var pfn = bangIdx >= 0 ? aumid[..bangIdx] : aumid;

            var packageManager = new PackageManager();
            var packages = packageManager.FindPackagesForUser("", pfn);

            foreach (var pkg in packages)
            {
                try
                {
                    var appEntries = pkg.GetAppListEntriesAsync().AsTask().GetAwaiter().GetResult();
                    foreach (var entry in appEntries)
                    {
                        if (string.Equals(entry.AppUserModelId, aumid, StringComparison.OrdinalIgnoreCase))
                        {
                            var name = entry.DisplayInfo.DisplayName;
                            if (!string.IsNullOrWhiteSpace(name))
                            {
                                MetadataCache[aumid] = (name, null);
                                return name;
                            }
                        }
                    }

                    // Fallback to package display name
                    if (!string.IsNullOrWhiteSpace(pkg.DisplayName))
                    {
                        MetadataCache[aumid] = (pkg.DisplayName, null);
                        return pkg.DisplayName;
                    }
                }
                catch { }
            }
        }
        catch { }

        return FallbackStoreAppName(aumid);
    }

    /// <summary>
    /// Loads the official Microsoft Store application icon as a WinUI 3 BitmapImage asynchronously.
    /// </summary>
    public static async Task<BitmapImage?> GetStoreAppIconAsync(string aumid)
    {
        if (string.IsNullOrEmpty(aumid)) return null;

        try
        {
            var bangIdx = aumid.IndexOf('!');
            var pfn = bangIdx >= 0 ? aumid[..bangIdx] : aumid;

            var packageManager = new PackageManager();
            var packages = packageManager.FindPackagesForUser("", pfn);

            foreach (var pkg in packages)
            {
                try
                {
                    var appEntries = await pkg.GetAppListEntriesAsync();
                    foreach (var entry in appEntries)
                    {
                        if (string.Equals(entry.AppUserModelId, aumid, StringComparison.OrdinalIgnoreCase))
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
                catch { }

                // Fallback to pkg.Logo
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
        catch { }

        return null;
    }

    /// <summary>
    /// Extracts a readable name from AUMID when package metadata is unavailable.
    /// </summary>
    public static string FallbackStoreAppName(string aumid)
    {
        try
        {
            var raw = aumid;
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
            return aumid;
        }
    }
}
