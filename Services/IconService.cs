using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace GPUAssign.Services;

/// <summary>
/// Extracts and caches application icons from EXE files and Microsoft Store packages for WinUI 3 display.
/// </summary>
public static class IconService
{
    private static readonly ConcurrentDictionary<string, byte[]> IconPngCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Extracts the icon from the given EXE path as PNG bytes (can run on background thread).
    /// Returns null if extraction fails.
    /// </summary>
    public static byte[]? ExtractIconBytes(string exePath)
    {
        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            return null;

        if (IconPngCache.TryGetValue(exePath, out var cached))
            return cached;

        try
        {
            using var icon = Icon.ExtractAssociatedIcon(exePath);
            if (icon == null) return null;

            using var bmp = icon.ToBitmap();
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            var bytes = ms.ToArray();

            IconPngCache[exePath] = bytes;
            return bytes;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Creates a WinUI 3 BitmapImage from PNG bytes (must run on UI thread).
    /// </summary>
    public static async Task<BitmapImage?> CreateBitmapFromBytesAsync(byte[]? pngBytes)
    {
        if (pngBytes == null || pngBytes.Length == 0) return null;

        try
        {
            var bitmapImage = new BitmapImage();
            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(pngBytes.AsBuffer());
            stream.Seek(0);
            await bitmapImage.SetSourceAsync(stream);
            return bitmapImage;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Loads the icon for an EXE file or Microsoft Store app as a WinUI 3 BitmapImage asynchronously.
    /// </summary>
    public static async Task<BitmapImage?> GetAppIconAsync(string? targetPathOrAumid)
    {
        if (string.IsNullOrEmpty(targetPathOrAumid)) return null;

        // 1. If this is a Microsoft Store packaged application
        if (StoreAppService.IsStoreAppId(targetPathOrAumid))
        {
            return await StoreAppService.GetStoreAppIconAsync(targetPathOrAumid);
        }

        // 2. Standard Win32 EXE
        var bytes = await Task.Run(() => ExtractIconBytes(targetPathOrAumid));
        if (bytes == null) return null;

        return await CreateBitmapFromBytesAsync(bytes);
    }
}
