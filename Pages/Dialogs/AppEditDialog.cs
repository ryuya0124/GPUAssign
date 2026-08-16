using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GPUAssign.Models;
using GPUAssign.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace GPUAssign.Pages.Dialogs;

/// <summary>
/// Dialog for adding or editing an application.
/// Provides live path resolution, directory opening, and background EXE discovery testing.
/// </summary>
public sealed class AppEditDialog : ContentDialog
{
    public AppDefinition? ResultApp { get; private set; }

    private readonly AppDefinition _editing;

    private readonly TextBox      _nameBox;
    private readonly TextBox      _categoryBox;
    private readonly ComboBox     _searchModeCombo;
    private readonly TextBox      _searchPathBox;
    private readonly TextBox      _exeNameBox;
    private readonly ToggleSwitch _recursiveToggle;
    private readonly ComboBox     _gpuComboBox;

    private readonly TextBlock    _previewText;
    private readonly TextBlock    _modeHintText;
    private readonly TextBlock    _exeLabelText;
    private readonly StackPanel   _searchPathSection;
    private readonly Border       _previewCard;

    private readonly Button       _openFolderBtn;
    private readonly Button       _testDiscoveryBtn;
    private readonly Button       _openDetectedExeBtn;
    private readonly ProgressRing _testProgressRing;
    private readonly TextBlock    _testResultText;

    private string? _lastDetectedExePath;

    public AppEditDialog(AppDefinition? existing)
    {
        Title             = existing is null ? L.Get("dialog.addApp.title") : L.Get("dialog.editApp.title");
        PrimaryButtonText = L.Get("action.save");
        CloseButtonText   = L.Get("action.cancel");
        DefaultButton     = ContentDialogButton.Primary;
        IsPrimaryButtonEnabled = false;

        _editing = existing ?? new AppDefinition();

        // ── Controls ─────────────────────────────────────────────────────

        _nameBox = new TextBox
        {
            Header          = L.Get("field.appName"),
            PlaceholderText = L.Get("field.appName.placeholder"),
            Text            = _editing.Name
        };

        _categoryBox = new TextBox
        {
            Header          = L.Get("field.category"),
            PlaceholderText = "例: コミュニケーション, ゲーム, ブラウザ",
            Text            = _editing.Category
        };

        _searchModeCombo = new ComboBox
        {
            Header              = L.Get("field.searchMode"),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        foreach (SearchMode mode in Enum.GetValues<SearchMode>())
        {
            _searchModeCombo.Items.Add(new ComboBoxItem
            {
                Content = GetSearchModeLabel(mode),
                Tag     = mode
            });
        }
        _searchModeCombo.SelectedIndex = (int)_editing.SearchMode;

        _modeHintText = new TextBlock
        {
            FontSize     = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin       = new Thickness(0, 2, 0, 0),
            Opacity      = 0.7
        };

        _searchPathBox = new TextBox
        {
            Header          = L.Get("field.searchPath"),
            PlaceholderText = L.Get("field.searchPath.placeholder"),
            Text            = _editing.SearchPath
        };

        var pathHint = new TextBlock
        {
            Text         = L.Get("field.searchPath.hint"),
            FontSize     = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin       = new Thickness(0, 2, 0, 0),
            Opacity      = 0.7
        };

        _openFolderBtn = new Button
        {
            Content = "📁 フォルダをエクスプローラーで開く",
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4, 0, 0)
        };
        _openFolderBtn.Click += OpenFolderBtn_Click;

        _searchPathSection = new StackPanel
        {
            Spacing = 2,
            Children = { _searchPathBox, pathHint, _openFolderBtn }
        };

        _exeLabelText = new TextBlock
        {
            Text   = L.Get("field.exe"),
            Margin = new Thickness(0, 0, 0, 4)
        };
        _exeNameBox = new TextBox
        {
            PlaceholderText = L.Get("field.exe.placeholder"),
            Text            = _editing.ExeName
        };

        _recursiveToggle = new ToggleSwitch
        {
            Header = L.Get("field.recursive"),
            IsOn   = _editing.Recursive
        };

        _gpuComboBox = new ComboBox
        {
            Header              = L.Get("field.gpu"),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _gpuComboBox.Items.Add(new ComboBoxItem { Content = L.Get("gpu.default"),      Tag = "Default" });
        _gpuComboBox.Items.Add(new ComboBoxItem { Content = L.Get("gpu.powerSaving"),   Tag = "PowerSaving" });
        _gpuComboBox.Items.Add(new ComboBoxItem { Content = L.Get("gpu.high"),          Tag = "HighPerformance" });
        _gpuComboBox.SelectedIndex = (int)_editing.GpuPreference;

        // Path preview text
        _previewText = new TextBlock
        {
            Text         = "─",
            FontSize     = 12,
            TextWrapping = TextWrapping.Wrap,
            Opacity      = 0.8
        };

        // Test discovery section (verifies if EXE actually exists)
        _testDiscoveryBtn = new Button
        {
            Content = "🔍 EXEの存在確認・検出テスト",
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _testDiscoveryBtn.Click += TestDiscoveryBtn_Click;

        _openDetectedExeBtn = new Button
        {
            Content = "📂 検出されたEXEを開く",
            HorizontalAlignment = HorizontalAlignment.Left,
            Visibility = Visibility.Collapsed
        };
        _openDetectedExeBtn.Click += OpenDetectedExeBtn_Click;

        _testProgressRing = new ProgressRing
        {
            Width = 20,
            Height = 20,
            IsActive = false,
            Visibility = Visibility.Collapsed
        };

        _testResultText = new TextBlock
        {
            FontSize     = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin       = new Thickness(0, 4, 0, 0)
        };

        var testActionRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing     = 10,
            Children = { _testDiscoveryBtn, _openDetectedExeBtn, _testProgressRing }
        };

        _previewCard = new Border
        {
            Padding         = new Thickness(14),
            CornerRadius    = new CornerRadius(8),
            Background      = new SolidColorBrush(Windows.UI.Color.FromArgb(20, 128, 128, 128)),
            BorderThickness = new Thickness(1),
            BorderBrush     = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 128, 128, 128)),
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = L.Get("field.preview"), FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                    _previewText,
                    testActionRow,
                    _testResultText
                }
            }
        };

        var exeSection = new StackPanel
        {
            Spacing = 4,
            Children = { _exeLabelText, _exeNameBox }
        };

        var panel = new StackPanel { Width = 440, Spacing = 14 };
        panel.Children.Add(_nameBox);
        panel.Children.Add(_categoryBox);
        panel.Children.Add(new StackPanel { Spacing = 4, Children = { _searchModeCombo, _modeHintText } });
        panel.Children.Add(_searchPathSection);
        panel.Children.Add(exeSection);
        panel.Children.Add(_recursiveToggle);
        panel.Children.Add(_gpuComboBox);
        panel.Children.Add(_previewCard);

        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = panel
        };

        // ── Wire events ───────────────────────────────────────────────────
        _nameBox.TextChanged              += (_, _) => Validate();
        _searchPathBox.TextChanged        += (_, _) => { UpdatePreview(); Validate(); };
        _exeNameBox.TextChanged           += (_, _) => { UpdatePreview(); Validate(); };
        _searchModeCombo.SelectionChanged += (_, _) => UpdateModeUI();

        PrimaryButtonClick += OnSave;

        UpdateModeUI();
        Validate();
    }

    private SearchMode SelectedMode =>
        _searchModeCombo.SelectedItem is ComboBoxItem ci && ci.Tag is SearchMode m ? m : SearchMode.LatestVersion;

    private void UpdateModeUI()
    {
        var mode = SelectedMode;

        _modeHintText.Text = mode switch
        {
            SearchMode.Fixed         => L.Get("searchMode.fixed.hint"),
            SearchMode.LatestVersion => L.Get("searchMode.latestVersion.hint"),
            SearchMode.Glob          => L.Get("searchMode.glob.hint"),
            SearchMode.Regex         => L.Get("searchMode.regex.hint"),
            SearchMode.StoreApp      => "Microsoft Store / UWP アプリです。パス指定は不要で、パッケージ識別子(AUMID)で管理されます。",
            _                        => string.Empty
        };

        if (mode == SearchMode.StoreApp)
        {
            _searchPathSection.Visibility = Visibility.Collapsed;
            _previewCard.Visibility       = Visibility.Collapsed;
            _recursiveToggle.Visibility   = Visibility.Collapsed;
            _exeLabelText.Text            = "パッケージ識別子 (AUMID / PackageFamilyName)";
            _exeNameBox.PlaceholderText   = "例: Microsoft.Windows.Photos_8wekyb3d8bbwe!App";
        }
        else
        {
            _searchPathSection.Visibility = Visibility.Visible;
            _previewCard.Visibility       = Visibility.Visible;
            _recursiveToggle.Visibility   = mode is SearchMode.LatestVersion or SearchMode.Regex
                ? Visibility.Visible : Visibility.Collapsed;
            _exeLabelText.Text            = mode == SearchMode.Regex
                ? $"{L.Get("field.exe")} (正規表現パターン)"
                : L.Get("field.exe");
            _exeNameBox.PlaceholderText   = L.Get("field.exe.placeholder");
        }

        UpdatePreview();
        Validate();
    }

    private void UpdatePreview()
    {
        try
        {
            if (SelectedMode == SearchMode.StoreApp)
            {
                _previewText.Text = _exeNameBox.Text.Trim();
                return;
            }

            var expanded = ExeDiscoveryService.ExpandPath(_searchPathBox.Text.Trim());
            var exe      = _exeNameBox.Text.Trim();

            _previewText.Text = SelectedMode == SearchMode.Fixed && expanded.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? expanded
                : string.IsNullOrEmpty(exe)
                    ? expanded
                    : Path.Combine(expanded, exe);
        }
        catch
        {
            _previewText.Text = "─";
        }
    }

    private void Validate()
    {
        if (SelectedMode == SearchMode.StoreApp)
        {
            IsPrimaryButtonEnabled =
                !string.IsNullOrWhiteSpace(_nameBox.Text) &&
                !string.IsNullOrWhiteSpace(_exeNameBox.Text);
        }
        else
        {
            IsPrimaryButtonEnabled =
                !string.IsNullOrWhiteSpace(_nameBox.Text) &&
                !string.IsNullOrWhiteSpace(_searchPathBox.Text) &&
                !string.IsNullOrWhiteSpace(_exeNameBox.Text);
        }
    }

    private void OpenFolderBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = ExeDiscoveryService.ExpandPath(_searchPathBox.Text.Trim());
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
                _testResultText.Text = $"⚠ ディレクトリが存在しません: {path}";
                _testResultText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 239, 68, 68));
            }
        }
        catch (Exception ex)
        {
            _testResultText.Text = $"エラー: {ex.Message}";
        }
    }

    private async void TestDiscoveryBtn_Click(object sender, RoutedEventArgs e)
    {
        _testDiscoveryBtn.IsEnabled = false;
        _testProgressRing.Visibility = Visibility.Visible;
        _testProgressRing.IsActive = true;
        _testResultText.Text = "探索中...";
        _testResultText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 150, 150, 150));
        _openDetectedExeBtn.Visibility = Visibility.Collapsed;
        _lastDetectedExePath = null;

        var tempApp = new AppDefinition
        {
            Name       = _nameBox.Text.Trim(),
            SearchPath = _searchPathBox.Text.Trim(),
            ExeName    = _exeNameBox.Text.Trim(),
            SearchMode = SelectedMode,
            Recursive  = _recursiveToggle.IsOn
        };

        var (bestMatch, allMatches) = await Task.Run(() =>
        {
            var matches = ExeDiscoveryService.FindAllMatches(tempApp);
            var best = ExeDiscoveryService.FindBestMatch(tempApp);
            return (best, matches);
        });

        _testProgressRing.IsActive = false;
        _testProgressRing.Visibility = Visibility.Collapsed;
        _testDiscoveryBtn.IsEnabled = true;

        if (bestMatch != null)
        {
            _lastDetectedExePath = bestMatch;
            _openDetectedExeBtn.Visibility = Visibility.Visible;
            _testResultText.Text = $"✓ 検出成功 ({allMatches.Count} 件中 最適):\n{bestMatch}";
            _testResultText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 34, 197, 94));
        }
        else
        {
            _testResultText.Text = "✗ 該当するEXEが見つかりませんでした。\n検索ディレクトリとEXE名・検索モードを確認してください。";
            _testResultText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 239, 68, 68));
        }
    }

    private void OpenDetectedExeBtn_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_lastDetectedExePath) || !File.Exists(_lastDetectedExePath)) return;
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_lastDetectedExePath}\"") { UseShellExecute = true });
        }
        catch { }
    }

    private void OnSave(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        _editing.Name       = _nameBox.Text.Trim();
        _editing.Category   = _categoryBox.Text.Trim();
        _editing.SearchPath = SelectedMode == SearchMode.StoreApp ? string.Empty : _searchPathBox.Text.Trim();
        _editing.ExeName    = _exeNameBox.Text.Trim();
        _editing.Recursive  = _recursiveToggle.IsOn;
        _editing.SearchMode = SelectedMode;

        if (_gpuComboBox.SelectedItem is ComboBoxItem sel)
        {
            _editing.GpuPreference = (sel.Tag as string) switch
            {
                "PowerSaving"    => GpuPreference.PowerSaving,
                "HighPerformance"=> GpuPreference.HighPerformance,
                _                => GpuPreference.Default
            };
        }

        ResultApp = _editing;
    }

    private static string GetSearchModeLabel(SearchMode mode) => mode switch
    {
        SearchMode.Fixed         => L.Get("searchMode.fixed"),
        SearchMode.LatestVersion => L.Get("searchMode.latestVersion"),
        SearchMode.Glob          => L.Get("searchMode.glob"),
        SearchMode.Regex         => L.Get("searchMode.regex"),
        SearchMode.StoreApp      => "Microsoft Store アプリ",
        _                        => mode.ToString()
    };
}
