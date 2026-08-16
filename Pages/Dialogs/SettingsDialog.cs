using System;
using System.Diagnostics;
using System.IO;
using GPUAssign.Models;
using GPUAssign.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GPUAssign.Pages.Dialogs;

/// <summary>
/// Settings dialog opened from the toolbar.
/// Configures threads, theme, language, auto-start, and cleanup.
/// </summary>
public sealed class SettingsDialog : ContentDialog
{
    private readonly AppConfig _config = ConfigService.Load();
    private readonly ComboBox _themeComboBox;
    private readonly ComboBox _languageComboBox;
    private readonly ComboBox _threadsComboBox;
    private readonly ToggleSwitch _autoStartToggle;
    private readonly ToggleSwitch _autoCleanupToggle;
    private readonly TextBlock _statusText;

    public SettingsDialog()
    {
        Title             = L.Get("page.settings.title");
        CloseButtonText   = L.Get("action.close");
        DefaultButton     = ContentDialogButton.Close;

        var panel = new StackPanel { Width = 440, Spacing = 14 };

        // 1. Threads
        _threadsComboBox = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        _threadsComboBox.Items.Add(new ComboBoxItem { Content = "1 (シングルスレッド)", Tag = "1" });
        _threadsComboBox.Items.Add(new ComboBoxItem { Content = "2 スレッド", Tag = "2" });
        _threadsComboBox.Items.Add(new ComboBoxItem { Content = "4 スレッド (推奨・デフォルト)", Tag = "4" });
        _threadsComboBox.Items.Add(new ComboBoxItem { Content = "8 スレッド (高速)", Tag = "8" });
        _threadsComboBox.Items.Add(new ComboBoxItem { Content = "16 スレッド (最大)", Tag = "16" });

        var threadTag = _config.MaxDegreeOfParallelism.ToString();
        foreach (ComboBoxItem item in _threadsComboBox.Items)
        {
            if (item.Tag as string == threadTag) { _threadsComboBox.SelectedItem = item; break; }
        }
        if (_threadsComboBox.SelectedItem == null) _threadsComboBox.SelectedIndex = 2;

        _threadsComboBox.SelectionChanged += (_, _) =>
        {
            if (_threadsComboBox.SelectedItem is ComboBoxItem ci && int.TryParse(ci.Tag as string, out int t))
            {
                _config.MaxDegreeOfParallelism = t;
                ConfigService.Save(_config);
            }
        };

        panel.Children.Add(new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock { Text = L.Get("page.settings.threads.label"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                new TextBlock { Text = L.Get("page.settings.threads.desc"), FontSize = 11, Opacity = 0.7 },
                _threadsComboBox
            }
        });

        // 2. Theme
        _themeComboBox = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        _themeComboBox.Items.Add(new ComboBoxItem { Content = L.Get("page.settings.theme.system"), Tag = "System" });
        _themeComboBox.Items.Add(new ComboBoxItem { Content = L.Get("page.settings.theme.light"), Tag = "Light" });
        _themeComboBox.Items.Add(new ComboBoxItem { Content = L.Get("page.settings.theme.dark"), Tag = "Dark" });

        foreach (ComboBoxItem item in _themeComboBox.Items)
        {
            if (item.Tag as string == _config.Theme) { _themeComboBox.SelectedItem = item; break; }
        }
        if (_themeComboBox.SelectedItem == null) _themeComboBox.SelectedIndex = 0;

        _themeComboBox.SelectionChanged += (_, _) =>
        {
            if (_themeComboBox.SelectedItem is ComboBoxItem ci && ci.Tag is string theme)
            {
                _config.Theme = theme;
                ConfigService.Save(_config);
                if (MainWindow.Current?.Content is FrameworkElement root)
                {
                    App.ApplyTheme(root, theme);
                }
            }
        };

        panel.Children.Add(new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock { Text = L.Get("page.settings.theme.label"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                _themeComboBox
            }
        });

        // 3. Language
        _languageComboBox = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        _languageComboBox.Items.Add(new ComboBoxItem { Content = L.Get("page.settings.language.auto"), Tag = "auto" });
        _languageComboBox.Items.Add(new ComboBoxItem { Content = "日本語 (ja-JP)", Tag = "ja-JP" });
        _languageComboBox.Items.Add(new ComboBoxItem { Content = "English (en-US)", Tag = "en-US" });

        foreach (ComboBoxItem item in _languageComboBox.Items)
        {
            if (item.Tag as string == _config.Language) { _languageComboBox.SelectedItem = item; break; }
        }
        if (_languageComboBox.SelectedItem == null) _languageComboBox.SelectedIndex = 0;

        _languageComboBox.SelectionChanged += (_, _) =>
        {
            if (_languageComboBox.SelectedItem is ComboBoxItem ci && ci.Tag is string lang)
            {
                _config.Language = lang;
                ConfigService.Save(_config);
                LocalizationService.Initialize(lang);
                MainWindow.Current?.UpdateLocalizedStrings();
            }
        };

        panel.Children.Add(new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock { Text = L.Get("page.settings.language.label"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                _languageComboBox
            }
        });

        // 4. Auto start on logon
        _autoStartToggle = new ToggleSwitch
        {
            Header = L.Get("page.settings.autoStart.label"),
            IsOn   = StartupService.IsStartupEnabled()
        };
        _autoStartToggle.Toggled += (_, _) =>
        {
            try
            {
                var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                if (_autoStartToggle.IsOn)
                {
                    StartupService.EnableStartupTask(exePath);
                }
                else
                {
                    StartupService.DisableStartupTask();
                }
            }
            catch { }
        };
        panel.Children.Add(_autoStartToggle);

        // 5. Auto cleanup stale entries
        _autoCleanupToggle = new ToggleSwitch
        {
            Header = L.Get("page.settings.autoCleanup.label"),
            IsOn   = _config.AutoCleanup
        };
        _autoCleanupToggle.Toggled += (_, _) =>
        {
            _config.AutoCleanup = _autoCleanupToggle.IsOn;
            ConfigService.Save(_config);
        };
        panel.Children.Add(_autoCleanupToggle);

        // 6. Cleanup now button
        _statusText = new TextBlock { FontSize = 12, Opacity = 0.8, VerticalAlignment = VerticalAlignment.Center };

        var cleanupBtn = new Button
        {
            Content = L.Get("page.settings.cleanupNow"),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        cleanupBtn.Click += (_, _) =>
        {
            int total = 0;
            foreach (var app in _config.Apps)
            {
                var removed = SyncService.CleanupStaleEntries(app);
                total += removed.Count;
            }
            _statusText.Text = total > 0 ? L.F("status.cleanupDone", total) : L.Get("status.cleanupNone");
        };

        var cleanupRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Children = { cleanupBtn, _statusText }
        };
        panel.Children.Add(cleanupRow);

        // 7. Open data folder button
        var openFolderBtn = new Button
        {
            Content = "📂 保存先フォルダを開く (ポータブル)",
            HorizontalAlignment = HorizontalAlignment.Left
        };
        openFolderBtn.Click += (_, _) =>
        {
            var dir = ConfigService.ConfigDir;
            Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
        };
        panel.Children.Add(openFolderBtn);

        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = panel
        };
    }
}
