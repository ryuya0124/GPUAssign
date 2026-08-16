using System;
using System.Collections.ObjectModel;
using System.IO;
using GPUAssign.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GPUAssign.Pages;

public sealed class BackupEntry
{
    public string FilePath    { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string FileSizeLabel { get; set; } = string.Empty;
}

public sealed partial class BackupPage : Page
{
    private readonly ObservableCollection<BackupEntry> _entries = new();

    public BackupPage()
    {
        InitializeComponent();
        ApplyLocalization();
        BackupListView.ItemsSource = _entries;
        LoadBackups();
    }

    private void ApplyLocalization()
    {
        PageTitleText.Text = L.Get("page.backup.title");
        PageSubtitleText.Text = L.Get("page.backup.subtitle");
        InfoCardTitleText.Text = L.Get("page.backup.info.title");
        InfoCardBodyText.Text = L.Get("page.backup.info.body");
        BackupNowBtn.Label = L.Get("action.backup");
        RefreshBtn.Label = L.Get("action.refresh");
    }

    private void LoadBackups()
    {
        _entries.Clear();
        foreach (var path in BackupService.GetBackups())
        {
            var fi = new FileInfo(path);
            _entries.Add(new BackupEntry
            {
                FilePath    = path,
                DisplayName = Path.GetFileNameWithoutExtension(path),
                FileSizeLabel = $"{fi.Length / 1024.0:F1} KB  ·  {fi.LastWriteTime:yyyy/MM/dd HH:mm}"
            });
        }
    }

    private void BackupNowButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = BackupService.CreateBackup();
            ShowStatus(InfoBarSeverity.Success, L.Get("action.backup"),
                L.F("status.backupCreated", Path.GetFileName(path)));
            LoadBackups();
        }
        catch (Exception ex)
        {
            ShowStatus(InfoBarSeverity.Error, L.Get("action.backup"),
                L.F("status.backupFailed", ex.Message));
        }
    }

    private async void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var path = btn.Tag as string;
        if (string.IsNullOrEmpty(path)) return;

        var confirm = new ContentDialog
        {
            Title             = L.Get("dialog.restoreBackup.title"),
            Content           = L.F("dialog.restoreBackup.message", Path.GetFileNameWithoutExtension(path)),
            PrimaryButtonText = L.Get("action.restore"),
            CloseButtonText   = L.Get("action.cancel"),
            DefaultButton     = ContentDialogButton.Close,
            XamlRoot          = XamlRoot
        };

        var result = await confirm.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        try
        {
            BackupService.RestoreBackup(path);
            ShowStatus(InfoBarSeverity.Success, L.Get("action.restore"),
                L.Get("status.restoreDone"));
        }
        catch (Exception ex)
        {
            ShowStatus(InfoBarSeverity.Error, L.Get("status.restoreFailed"), ex.Message);
        }
    }

    private async void DeleteBackupButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var path = btn.Tag as string;
        if (string.IsNullOrEmpty(path)) return;

        var confirm = new ContentDialog
        {
            Title             = L.Get("dialog.deleteBackup.title"),
            Content           = L.F("dialog.deleteBackup.message", Path.GetFileNameWithoutExtension(path)),
            PrimaryButtonText = L.Get("action.delete"),
            CloseButtonText   = L.Get("action.cancel"),
            DefaultButton     = ContentDialogButton.Close,
            XamlRoot          = XamlRoot
        };

        var result = await confirm.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        try
        {
            BackupService.DeleteBackup(path);
            LoadBackups();
        }
        catch (Exception ex)
        {
            ShowStatus(InfoBarSeverity.Error, L.Get("action.delete"), ex.Message);
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
        => LoadBackups();

    private void ShowStatus(InfoBarSeverity severity, string title, string message)
    {
        StatusBar.Severity = severity;
        StatusBar.Title    = title;
        StatusBar.Message  = message;
        StatusBar.IsOpen   = true;
    }
}
