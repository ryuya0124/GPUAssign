using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GPUAssign.Models;
using GPUAssign.Pages.Dialogs;
using GPUAssign.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GPUAssign.Pages;

public sealed partial class AppsPage : Page
{
    public ObservableCollection<AppDefinition> AppItems { get; } = new();

    private AppConfig _config = ConfigService.Load();
    private AppDefinition? _selectedApp;

    public AppsPage()
    {
        InitializeComponent();
        ApplyLocalization();
        LoadApps();
    }

    private void ApplyLocalization()
    {
        PageTitleText.Text = L.Get("page.apps.title");
        PageSubtitleText.Text = L.Get("page.apps.subtitle");
        EmptyStateText.Text = L.Get("page.apps.empty");

        SyncButton.Label = L.Get("action.sync");
        AddButton.Label = L.Get("action.add");
        ImportExistingButton.Label = "Windows設定からインポート";
        OpenFolderButton.Label = "フォルダを開く";
        EditButton.Label = L.Get("action.edit");
        DeleteButton.Label = L.Get("action.delete");
        SettingsButton.Label = L.Get("nav.settings");
    }

    private void LoadApps()
    {
        AppItems.Clear();
        foreach (var app in _config.Apps)
        {
            AppItems.Add(app);
        }

        EmptyStatePanel.Visibility = AppItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        AppListView.Visibility = AppItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        // Asynchronously load real EXE icons for all apps
        _ = LoadAppIconsAsync();
    }

    private async Task LoadAppIconsAsync()
    {
        foreach (var app in AppItems.ToList())
        {
            if (app.IconSource != null) continue;

            var exePath = app.CurrentExePath;
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                exePath = await Task.Run(() => ExeDiscoveryService.FindBestMatch(app));
                if (exePath != null)
                {
                    app.CurrentExePath = exePath;
                }
            }

            if (!string.IsNullOrEmpty(exePath))
            {
                var icon = await IconService.GetAppIconAsync(exePath);
                if (icon != null)
                {
                    app.IconSource = icon;
                }
            }
        }
    }

    // ---- Click Item to Edit Directly ----

    private void AppListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is AppDefinition app)
        {
            OpenEditDialog(app);
        }
    }

    private void AppListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedApp = AppListView.SelectedItem as AppDefinition;
        OpenFolderButton.IsEnabled = _selectedApp is not null;
        EditButton.IsEnabled       = _selectedApp is not null;
        DeleteButton.IsEnabled     = _selectedApp is not null;
    }

    // ---- Inline GPU ComboBox Selection Changed ----

    private void RowGpuComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.Tag is AppDefinition app)
        {
            ConfigService.Save(_config);

            // If app already has a known target EXE, update registry immediately
            if (!string.IsNullOrEmpty(app.CurrentExePath) && File.Exists(app.CurrentExePath))
            {
                try
                {
                    GpuPreferenceService.SetPreference(app.CurrentExePath, app.GpuPreference);
                    app.SyncStatus = SyncStatus.Synced;
                }
                catch { }
            }
        }
    }

    // ---- Open App Folder in Explorer ----

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedApp is null) return;

        try
        {
            if (!string.IsNullOrEmpty(_selectedApp.CurrentExePath) && File.Exists(_selectedApp.CurrentExePath))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_selectedApp.CurrentExePath}\"") { UseShellExecute = true });
                return;
            }

            var path = ExeDiscoveryService.ExpandPath(_selectedApp.SearchPath);
            if (path.Contains('*') || path.Contains('?'))
            {
                var wildIdx = path.IndexOfAny(new[] { '*', '?' });
                path = path.Substring(0, wildIdx);
                path = Path.GetDirectoryName(path) ?? path;
            }

            if (Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
            }
            else
            {
                ShowStatus(InfoBarSeverity.Warning, "フォルダが見つかりません", $"ディレクトリが存在しません: {path}");
            }
        }
        catch (Exception ex)
        {
            ShowStatus(InfoBarSeverity.Error, "エラー", ex.Message);
        }
    }

    // ---- Sync (Multithreaded background processing) ----

    private async void SyncButton_Click(object sender, RoutedEventArgs e)
    {
        SyncButton.IsEnabled = false;
        SyncProgressBar.Visibility = Visibility.Visible;
        ShowStatus(InfoBarSeverity.Informational, L.Get("sync.syncing"), L.Get("sync.syncing"));

        try
        {
            var progress = new Progress<string>(msg =>
            {
                ShowStatus(InfoBarSeverity.Informational, L.Get("sync.syncing"), msg);
            });

            var results = await SyncService.SyncAllAsync(_config, progress);

            int updated  = results.Count(r => r.Changed);
            int notFound = results.Count(r => r.NewPath is null && r.ErrorMessage is null);
            int errors   = results.Count(r => r.ErrorMessage is not null);

            foreach (var r in results)
            {
                SyncLogPage.AppendLog(
                    r.AppName,
                    r.ErrorMessage is null,
                    r.ErrorMessage ?? (r.Changed ? $"更新 → {r.NewPath}" : "変更なし"));
            }

            var summary = L.F("sync.result", updated, notFound, errors);

            if (errors > 0)
                ShowStatus(InfoBarSeverity.Error, L.Get("sync.done"), summary);
            else
                ShowStatus(InfoBarSeverity.Success, L.Get("sync.done"), summary);

            LoadApps();
        }
        catch (Exception ex)
        {
            ShowStatus(InfoBarSeverity.Error, L.Get("sync.failed"), ex.Message);
        }
        finally
        {
            SyncProgressBar.Visibility = Visibility.Collapsed;
            SyncButton.IsEnabled = true;
        }
    }

    // ---- Import Existing Windows Graphics Settings ----

    private async void ImportExistingButton_Click(object sender, RoutedEventArgs e)
    {
        var detected = GpuPreferenceService.ScanExistingWindowsPreferences();

        var managedExeNames = _config.Apps.Select(a => a.ExeName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unmanaged = detected.Where(d => !managedExeNames.Contains(d.ExeName)).ToList();

        if (unmanaged.Count == 0)
        {
            ShowStatus(InfoBarSeverity.Informational, "Windows設定からインポート",
                detected.Count == 0
                    ? "Windowsのグラフィックス設定に登録されているアプリは見つかりませんでした。"
                    : "Windowsグラフィックス設定のすべてのアプリが既に取り込み済みです。");
            return;
        }

        var dialog = new ImportExistingDialog(unmanaged)
        {
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && dialog.SelectedApps.Count > 0)
        {
            foreach (var app in dialog.SelectedApps)
            {
                _config.Apps.Add(app);
                AppItems.Add(app);
            }
            ConfigService.Save(_config);
            EmptyStatePanel.Visibility = Visibility.Collapsed;
            AppListView.Visibility = Visibility.Visible;

            _ = LoadAppIconsAsync();

            ShowStatus(InfoBarSeverity.Success, "インポート完了", $"{dialog.SelectedApps.Count} 件のアプリをWindows設定から取り込みました。");
        }
    }

    // ---- Add Custom ----

    private async void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AppEditDialog(null)
        {
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && dialog.ResultApp is { } newApp)
        {
            _config.Apps.Add(newApp);
            ConfigService.Save(_config);
            AppItems.Add(newApp);
            EmptyStatePanel.Visibility = Visibility.Collapsed;
            AppListView.Visibility = Visibility.Visible;
            _ = LoadAppIconsAsync();
        }
    }

    // ---- Edit ----

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedApp != null)
        {
            OpenEditDialog(_selectedApp);
        }
    }

    private async void OpenEditDialog(AppDefinition app)
    {
        var dialog = new AppEditDialog(app)
        {
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            ConfigService.Save(_config);
            var idx = AppItems.IndexOf(app);
            if (idx >= 0)
            {
                AppItems.RemoveAt(idx);
                AppItems.Insert(idx, app);
                AppListView.SelectedItem = app;
            }
            _ = LoadAppIconsAsync();
        }
    }

    // ---- Delete ----

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedApp is null) return;

        var confirm = new ContentDialog
        {
            Title = L.Get("dialog.deleteApp.title"),
            Content = L.F("dialog.deleteApp.message", _selectedApp.Name),
            PrimaryButtonText = L.Get("action.delete"),
            CloseButtonText = L.Get("action.cancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        var result = await confirm.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            _config.Apps.Remove(_selectedApp);
            ConfigService.Save(_config);
            AppItems.Remove(_selectedApp);
            _selectedApp = null;
            OpenFolderButton.IsEnabled = false;
            EditButton.IsEnabled       = false;
            DeleteButton.IsEnabled     = false;

            if (AppItems.Count == 0)
            {
                EmptyStatePanel.Visibility = Visibility.Visible;
                AppListView.Visibility = Visibility.Collapsed;
            }
        }
    }

    // ---- Settings Dialog ----

    private async void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsDialog { XamlRoot = XamlRoot };
        await dialog.ShowAsync();
        _config = ConfigService.Load();
    }

    // ---- Helpers ----

    private void ShowStatus(InfoBarSeverity severity, string title, string message)
    {
        StatusBar.Severity = severity;
        StatusBar.Title = title;
        StatusBar.Message = message;
        StatusBar.IsOpen = true;
    }
}
