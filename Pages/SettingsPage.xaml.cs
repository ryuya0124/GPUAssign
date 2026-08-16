using System;
using System.Diagnostics;
using GPUAssign.Models;
using GPUAssign.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GPUAssign.Pages;

public sealed partial class SettingsPage : Page
{
    private AppConfig _config = ConfigService.Load();
    private bool _loading = true;

    public SettingsPage()
    {
        InitializeComponent();
        ApplyLocalization();
        LoadSettings();
    }

    private void ApplyLocalization()
    {
        PageTitleText.Text    = L.Get("page.settings.title");
        PageSubtitleText.Text = L.Get("page.settings.subtitle");

        ThreadsLabel.Text = L.Get("page.settings.threads.label");
        ThreadsDesc.Text  = L.Get("page.settings.threads.desc");

        ThemeLabel.Text      = L.Get("page.settings.theme.label");
        ThemeSystemItem.Content = L.Get("page.settings.theme.system");
        ThemeLightItem.Content  = L.Get("page.settings.theme.light");
        ThemeDarkItem.Content   = L.Get("page.settings.theme.dark");

        LanguageLabel.Text = L.Get("page.settings.language.label");
        LangAutoItem.Content = L.Get("page.settings.language.auto");

        AutoStartToggle.Header   = L.Get("page.settings.autoStart.label");
        AutoCleanupToggle.Header = L.Get("page.settings.autoCleanup.label");
        CleanupNowButton.Content = L.Get("page.settings.cleanupNow");
    }

    private void LoadSettings()
    {
        _loading = true;

        // Threads
        var threadTag = _config.MaxDegreeOfParallelism.ToString();
        foreach (ComboBoxItem item in ThreadsComboBox.Items)
        {
            if (item.Tag as string == threadTag)
            {
                ThreadsComboBox.SelectedItem = item;
                break;
            }
        }
        if (ThreadsComboBox.SelectedItem == null)
            ThreadsComboBox.SelectedIndex = 2; // default 4

        // Theme
        foreach (ComboBoxItem item in ThemeComboBox.Items)
        {
            if (item.Tag as string == _config.Theme)
            {
                ThemeComboBox.SelectedItem = item;
                break;
            }
        }
        if (ThemeComboBox.SelectedItem == null)
            ThemeComboBox.SelectedIndex = 0;

        // Language
        foreach (ComboBoxItem item in LanguageComboBox.Items)
        {
            if (item.Tag as string == _config.Language)
            {
                LanguageComboBox.SelectedItem = item;
                break;
            }
        }
        if (LanguageComboBox.SelectedItem == null)
            LanguageComboBox.SelectedIndex = 0;

        // Automation
        AutoStartToggle.IsOn   = StartupService.IsStartupEnabled();
        AutoCleanupToggle.IsOn = _config.AutoCleanup;

        // Storage path
        StoragePathText.Text = $"データ保存先: {ConfigService.ConfigDir} (apps.json, sync_log.json, backups/)";

        _loading = false;
    }

    private void ThreadsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (ThreadsComboBox.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag as string, out int threads))
        {
            _config.MaxDegreeOfParallelism = threads;
            ConfigService.Save(_config);
            ShowSaved();
        }
    }

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (ThemeComboBox.SelectedItem is ComboBoxItem item && item.Tag is string theme)
        {
            _config.Theme = theme;
            ConfigService.Save(_config);
            if (MainWindow.Current?.Content is FrameworkElement root)
            {
                App.ApplyTheme(root, theme);
            }
            ShowSaved();
        }
    }

    private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Tag is string lang)
        {
            _config.Language = lang;
            ConfigService.Save(_config);
            LocalizationService.Initialize(lang);
            ApplyLocalization();
            MainWindow.Current?.UpdateLocalizedStrings();
            ShowSaved();
        }
    }

    private void AutoStartToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        try
        {
            var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            if (AutoStartToggle.IsOn)
            {
                StartupService.EnableStartupTask(exePath);
                ShowStatus(InfoBarSeverity.Success, "自動同期を有効化しました", "次回ログオン時よりバックグラウンド同期が実行されます。");
            }
            else
            {
                StartupService.DisableStartupTask();
                ShowStatus(InfoBarSeverity.Informational, "自動同期を無効化しました", "");
            }
        }
        catch (Exception ex)
        {
            ShowStatus(InfoBarSeverity.Error, "エラー", ex.Message);
        }
    }

    private void AutoCleanupToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _config.AutoCleanup = AutoCleanupToggle.IsOn;
        ConfigService.Save(_config);
        ShowSaved();
    }

    private void CleanupNowButton_Click(object sender, RoutedEventArgs e)
    {
        int total = 0;
        foreach (var app in _config.Apps)
        {
            var removed = SyncService.CleanupStaleEntries(app);
            total += removed.Count;
        }

        if (total > 0)
            ShowStatus(InfoBarSeverity.Success, "整理完了", L.F("status.cleanupDone", total));
        else
            ShowStatus(InfoBarSeverity.Informational, "整理完了", L.Get("status.cleanupNone"));
    }

    private void OpenDataFolderButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", ConfigService.ConfigDir) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowStatus(InfoBarSeverity.Error, "エラー", ex.Message);
        }
    }

    private void ShowSaved()
    {
        ShowStatus(InfoBarSeverity.Success, L.Get("status.saved"), "");
    }

    private void ShowStatus(InfoBarSeverity severity, string title, string message)
    {
        SettingsStatusInfoBar.Severity = severity;
        SettingsStatusInfoBar.Title = title;
        SettingsStatusInfoBar.Message = message;
        SettingsStatusInfoBar.IsOpen = true;
    }
}
