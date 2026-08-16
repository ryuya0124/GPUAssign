using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using GPUAssign.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GPUAssign.Pages;

/// <summary>A single entry in the sync log.</summary>
public sealed class SyncLogEntry
{
    public string AppName    { get; set; } = string.Empty;
    public string Timestamp  { get; set; } = string.Empty;
    public string Message    { get; set; } = string.Empty;
    public bool   Success    { get; set; }

    public string StatusLabel => Success ? L.Get("sync.synced") : L.Get("sync.error");
    public string StatusColor => Success ? "#22C55E" : "#EF4444";
}

public sealed partial class SyncLogPage : Page
{
    public ObservableCollection<SyncLogEntry> LogEntries { get; } = new();

    private static string LogFilePath => Path.Combine(ConfigService.ConfigDir, "sync_log.json");

    public SyncLogPage()
    {
        InitializeComponent();
        ApplyLocalization();
        LogListView.ItemsSource = LogEntries;
        LoadLog();
    }

    private void ApplyLocalization()
    {
        PageTitleText.Text = L.Get("page.sync.title");
        PageSubtitleText.Text = L.Get("page.sync.subtitle");
        RunSyncBtn.Label = L.Get("action.sync");
        ClearLogBtn.Label = L.Get("action.clearLog");
    }

    private void LoadLog()
    {
        LogEntries.Clear();
        try
        {
            if (!File.Exists(LogFilePath)) return;
            var json    = File.ReadAllText(LogFilePath);
            var entries = JsonSerializer.Deserialize<SyncLogEntry[]>(json);
            if (entries is null) return;

            foreach (var e in entries.Reverse())
                LogEntries.Add(e);
        }
        catch { /* ignore */ }
    }

    internal static void AppendLog(string appName, bool success, string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath)!);

            var entries = new System.Collections.Generic.List<SyncLogEntry>();
            if (File.Exists(LogFilePath))
            {
                var existing = JsonSerializer.Deserialize<SyncLogEntry[]>(File.ReadAllText(LogFilePath));
                if (existing is not null) entries.AddRange(existing);
            }

            entries.Add(new SyncLogEntry
            {
                AppName   = appName,
                Timestamp = DateTime.Now.ToString("MM/dd HH:mm"),
                Message   = message,
                Success   = success
            });

            // Keep last 200 entries
            if (entries.Count > 200)
                entries = entries.TakeLast(200).ToList();

            File.WriteAllText(LogFilePath, JsonSerializer.Serialize(entries,
                new JsonSerializerOptions { WriteIndented = false }));
        }
        catch { /* ignore */ }
    }

    private async void RunSyncBtn_Click(object sender, RoutedEventArgs e)
    {
        RunSyncBtn.IsEnabled = false;
        var config = ConfigService.Load();

        try
        {
            var progress = new Progress<string>(msg =>
                StatusBar.Message = msg);

            StatusBar.Severity = InfoBarSeverity.Informational;
            StatusBar.Title    = L.Get("sync.syncing");
            StatusBar.IsOpen   = true;

            var results = await SyncService.SyncAllAsync(config, progress);

            foreach (var r in results)
            {
                AppendLog(
                    r.AppName,
                    r.ErrorMessage is null,
                    r.ErrorMessage ?? (r.Changed ? $"更新 → {r.NewPath}" : "変更なし"));
            }

            StatusBar.Severity = InfoBarSeverity.Success;
            StatusBar.Title    = L.Get("sync.done");
            StatusBar.Message  = $"{results.Count} 件処理しました";

            LoadLog();
        }
        catch (Exception ex)
        {
            StatusBar.Severity = InfoBarSeverity.Error;
            StatusBar.Title    = L.Get("sync.failed");
            StatusBar.Message  = ex.Message;
        }
        finally
        {
            RunSyncBtn.IsEnabled = true;
        }
    }

    private void ClearLogBtn_Click(object sender, RoutedEventArgs e)
    {
        try { if (File.Exists(LogFilePath)) File.Delete(LogFilePath); } catch { }
        LogEntries.Clear();
    }
}
