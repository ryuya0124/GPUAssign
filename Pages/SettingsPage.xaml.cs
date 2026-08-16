using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using GPUAssign.Models;
using GPUAssign.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GPUAssign.Pages;

public sealed partial class SettingsPage : Page
{
    private AppConfig _config = ConfigService.Load();
    private bool _isLoading = true;

    public SettingsPage()
    {
        InitializeComponent();
        ApplyLocalization();
        LoadSettings();
    }

    private void ApplyLocalization()
    {
        PageTitleText.Text = L.Get("page.settings.title");
        PageSubtitleText.Text = L.Get("page.settings.subtitle");

        SectionAppearanceText.Text = L.Get("page.settings.section.appearance");
        ThemeLabelText.Text = L.Get("page.settings.theme.label");
        ThemeSystemItem.Content = L.Get("page.settings.theme.system");
        ThemeLightItem.Content = L.Get("page.settings.theme.light");
        ThemeDarkItem.Content = L.Get("page.settings.theme.dark");

        SectionLanguageText.Text = L.Get("page.settings.section.language");
        LanguageLabelText.Text = L.Get("page.settings.language.label");
        LangAutoItem.Content = L.Get("page.settings.language.auto");

        SectionPerformanceText.Text = L.Get("page.settings.section.performance");
        ThreadsLabelText.Text = L.Get("page.settings.threads.label");
        ThreadsDescText.Text = L.Get("page.settings.threads.desc");

        SectionAutoSyncText.Text = L.Get("page.settings.section.autoSync");
        AutoStartLabelText.Text = L.Get("page.settings.autoStart.label");
        AutoStartDescText.Text = L.Get("page.settings.autoStart.desc");

        SectionCleanupText.Text = L.Get("page.settings.section.cleanup");
        AutoCleanupLabelText.Text = L.Get("page.settings.autoCleanup.label");
        AutoCleanupDescText.Text = L.Get("page.settings.autoCleanup.desc");
        CleanupNowButton.Content = L.Get("page.settings.cleanupNow");

        SectionDataText.Text = L.Get("page.settings.section.data");
        ConfigPathLabelText.Text = L.Get("page.settings.configPath.label");
        OpenFolderButton.Content = L.Get("page.settings.openFolder");

        SectionAboutText.Text = L.Get("page.settings.section.about");
        AboutVersionText.Text = L.Get("page.settings.about.version");
        AboutDescText.Text = L.Get("page.settings.about.desc");
    }

    private void LoadSettings()
    {
        _isLoading = true;

        AutoCleanupToggle.IsOn = _config.AutoCleanup;
        AutoStartToggle.IsOn = StartupService.IsStartupEnabled();
        ConfigPathText.Text = ConfigService.ConfigFilePath;

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
        {
            // Default 4 (index 2)
            ThreadsComboBox.SelectedIndex = 2;
        }

        _isLoading = false;
    }

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;
        if (ThemeComboBox.SelectedItem is ComboBoxItem item && item.Tag is string theme)
        {
            _config.Theme = theme;
            ConfigService.Save(_config);
            if (MainWindow.Current?.Content is FrameworkElement root)
            {
                App.ApplyTheme(root, theme);
            }
        }
    }

    private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;
        if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Tag is string lang)
        {
            _config.Language = lang;
            ConfigService.Save(_config);

            // Re-initialize localization
            LocalizationService.Initialize(lang);

            // Update UI
            ApplyLocalization();
            MainWindow.Current?.UpdateLocalizedStrings();

            ShowStatus(InfoBarSeverity.Success, L.Get("status.settingSaved"), L.Get("status.settingSaved"));
        }
    }

    private void ThreadsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;
        if (ThreadsComboBox.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag as string, out int threads))
        {
            _config.MaxDegreeOfParallelism = threads;
            ConfigService.Save(_config);
            ShowStatus(InfoBarSeverity.Success, L.Get("status.settingSaved"), $"{L.Get("page.settings.threads.label")}: {threads}");
        }
    }

    private void AutoCleanupToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        _config.AutoCleanup = AutoCleanupToggle.IsOn;
        ConfigService.Save(_config);
        ShowStatus(InfoBarSeverity.Success, L.Get("status.settingSaved"),
            AutoCleanupToggle.IsOn ? L.Get("page.settings.autoCleanup.desc") : L.Get("status.settingSaved"));
    }

    private void AutoStartToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        try
        {
            var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;

            if (AutoStartToggle.IsOn)
            {
                StartupService.EnableStartupTask(exePath);
                ShowStatus(InfoBarSeverity.Success, L.Get("status.autoStartEnabled"), L.Get("page.settings.autoStart.desc"));
            }
            else
            {
                StartupService.DisableStartupTask();
                ShowStatus(InfoBarSeverity.Success, L.Get("status.autoStartDisabled"), L.Get("status.autoStartDisabled"));
            }
        }
        catch (Exception ex)
        {
            ShowStatus(InfoBarSeverity.Error, L.Get("status.autoStartFailed"), ex.Message);
            _isLoading = true;
            AutoStartToggle.IsOn = !AutoStartToggle.IsOn; // revert
            _isLoading = false;
        }
    }

    private void CleanupNowButton_Click(object sender, RoutedEventArgs e)
    {
        int total = 0;
        foreach (var app in _config.Apps)
        {
            var removed = SyncService.CleanupStaleEntries(app);
            total += removed.Count;
        }

        ShowStatus(InfoBarSeverity.Success, L.Get("action.cleanup"),
            total > 0 ? L.F("status.cleanupDone", total) : L.Get("status.cleanupNone"));
    }

    private void OpenConfigFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var dir = ConfigService.ConfigDir;
        Directory.CreateDirectory(dir);
        Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
    }

    private void ShowStatus(InfoBarSeverity severity, string title, string message)
    {
        StatusBar.Severity = severity;
        StatusBar.Title = title;
        StatusBar.Message = message;
        StatusBar.IsOpen = true;
    }
}
