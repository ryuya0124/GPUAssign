using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GPUAssign.Models;

namespace GPUAssign.Services;

/// <summary>Result of a single app sync operation.</summary>
public record AppSyncResult(
    string AppName,
    bool   Changed,
    string? OldPath,
    string? NewPath,
    List<string> RemovedPaths,
    string? ErrorMessage);

/// <summary>
/// Orchestrates the full sync cycle using multicore parallel processing
/// with configurable thread degree of parallelism.
/// </summary>
public static class SyncService
{
    /// <summary>
    /// Sync all registered apps in parallel using the specified MaxDegreeOfParallelism.
    /// Each app discovery runs throttled by a SemaphoreSlim; registry writes are serialized.
    /// </summary>
    public static async Task<List<AppSyncResult>> SyncAllAsync(
        AppConfig config,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        int maxConcurrency = Math.Clamp(config.MaxDegreeOfParallelism, 1, 32);
        using var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

        // Phase 1: Discover all EXEs in parallel with throttled degree of parallelism
        var discoveryTasks = config.Apps.Select(async app =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(L.F("sync.progress", app.Name));
                var exe = await Task.Run(() => ExeDiscoveryService.FindBestMatch(app), cancellationToken);
                return (app, exe);
            }
            finally
            {
                semaphore.Release();
            }
        }).ToList();

        var discoveries = await Task.WhenAll(discoveryTasks);

        // Phase 2: Registry operations (serialized on caller thread for registry safety)
        var results = new List<AppSyncResult>();
        foreach (var (app, bestExe) in discoveries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = ApplySync(app, bestExe, config.AutoCleanup);
            results.Add(result);
        }

        // Phase 3: Persist updated managed paths
        await Task.Run(() => ConfigService.Save(config), cancellationToken);

        return results;
    }

    /// <summary>Sync a single app (used from the UI for one-off updates).</summary>
    public static AppSyncResult SyncApp(AppDefinition app, bool autoCleanup)
    {
        var bestExe = ExeDiscoveryService.FindBestMatch(app);
        return ApplySync(app, bestExe, autoCleanup);
    }

    /// <summary>Remove stale registry entries for a single app.</summary>
    public static List<string> CleanupStaleEntries(AppDefinition app)
    {
        var removed  = new List<string>();
        var allPrefs = GpuPreferenceService.GetAllPreferences();
        var current  = app.CurrentExePath ?? ExeDiscoveryService.FindBestMatch(app);

        var stale = app.ManagedPaths
            .Where(p => current is null || !p.Equals(current, StringComparison.OrdinalIgnoreCase))
            .Where(p => allPrefs.ContainsKey(p))
            .ToList();

        foreach (var p in stale)
        {
            GpuPreferenceService.RemovePreference(p);
            removed.Add(p);
        }
        return removed;
    }

    // ──────────────────────────────────────────────
    // Private
    // ──────────────────────────────────────────────

    private static AppSyncResult ApplySync(AppDefinition app, string? bestExe, bool autoCleanup)
    {
        try
        {
            var removedPaths = new List<string>();

            if (bestExe is null)
            {
                app.SyncStatus  = SyncStatus.NotFound;
                app.SyncMessage = L.Get("sync.notFound");
                return new AppSyncResult(app.Name, false, null, null, removedPaths, L.Get("sync.notFound"));
            }

            app.CurrentExePath = bestExe;

            // Track this path
            if (!app.ManagedPaths.Contains(bestExe, StringComparer.OrdinalIgnoreCase))
                app.ManagedPaths.Add(bestExe);

            // Check current registry state
            var currentPref = GpuPreferenceService.GetPreference(bestExe);
            bool needsUpdate = currentPref != app.GpuPreference;

            // Cleanup stale entries
            if (autoCleanup)
            {
                var allPrefs = GpuPreferenceService.GetAllPreferences();
                var stale = app.ManagedPaths
                    .Where(p => !p.Equals(bestExe, StringComparison.OrdinalIgnoreCase))
                    .Where(p => allPrefs.ContainsKey(p))
                    .ToList();

                foreach (var s in stale)
                {
                    GpuPreferenceService.RemovePreference(s);
                    removedPaths.Add(s);
                }
            }

            if (needsUpdate)
                GpuPreferenceService.SetPreference(bestExe, app.GpuPreference);

            app.SyncStatus  = SyncStatus.Synced;
            app.SyncMessage = needsUpdate
                ? $"更新 → {Path.GetFileName(Path.GetDirectoryName(bestExe) ?? "")}/{Path.GetFileName(bestExe)}"
                : "変更なし";

            return new AppSyncResult(app.Name, needsUpdate, null, bestExe, removedPaths, null);
        }
        catch (Exception ex)
        {
            app.SyncStatus  = SyncStatus.Error;
            app.SyncMessage = ex.Message;
            return new AppSyncResult(app.Name, false, null, null, new(), ex.Message);
        }
    }
}
