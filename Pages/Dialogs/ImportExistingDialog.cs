using System;
using System.Collections.Generic;
using System.Linq;
using GPUAssign.Models;
using GPUAssign.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GPUAssign.Pages.Dialogs;

/// <summary>
/// Dialog allowing users to import existing GPU preferences configured
/// in standard Windows Graphics Settings into GPU Assign for automated version tracking.
/// </summary>
public sealed class ImportExistingDialog : ContentDialog
{
    public List<AppDefinition> SelectedApps { get; } = new();

    private readonly List<(CheckBox cb, AppDefinition app)> _rows = new();

    public ImportExistingDialog(List<AppDefinition> detectedApps)
    {
        Title             = "Windows既存設定からインポート";
        PrimaryButtonText = "選択したアプリを取り込む";
        CloseButtonText   = L.Get("action.cancel");
        DefaultButton     = ContentDialogButton.Primary;
        IsPrimaryButtonEnabled = detectedApps.Count > 0;

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = 520
        };

        var outer = new StackPanel { Width = 480, Spacing = 14 };

        outer.Children.Add(new TextBlock
        {
            Text         = "Windowsのグラフィックス設定で登録されているアプリを検出しました。\n管理対象にするアプリを選択してください (バージョンフォルダは自動でルール化されます):",
            TextWrapping = TextWrapping.Wrap,
            Opacity      = 0.8,
            FontSize     = 12
        });

        var selectAllRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        var selectAllBtn = new Button { Content = "すべて選択", FontSize = 12 };
        var unselectAllBtn = new Button { Content = "選択解除", FontSize = 12 };

        selectAllBtn.Click += (_, _) =>
        {
            foreach (var (cb, _) in _rows) cb.IsChecked = true;
            OnCheckChanged();
        };
        unselectAllBtn.Click += (_, _) =>
        {
            foreach (var (cb, _) in _rows) cb.IsChecked = false;
            OnCheckChanged();
        };

        selectAllRow.Children.Add(selectAllBtn);
        selectAllRow.Children.Add(unselectAllBtn);
        outer.Children.Add(selectAllRow);

        var listPanel = new StackPanel { Spacing = 8, Margin = new Thickness(0, 4, 0, 0) };

        foreach (var app in detectedApps)
        {
            var cb = new CheckBox
            {
                IsChecked = true, // default checked
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var modeLabel = new Border
            {
                CornerRadius    = new CornerRadius(4),
                Padding         = new Thickness(6, 1, 6, 1),
                Background      = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(40, 128, 128, 255)),
                Child = new TextBlock
                {
                    Text     = app.SearchModeLabel,
                    FontSize = 10,
                    Opacity  = 0.9
                }
            };

            var gpuBadge = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding      = new Thickness(6, 1, 6, 1),
                Background   = app.GpuPreference == GpuPreference.HighPerformance
                    ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(50, 34, 197, 94))
                    : new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(50, 59, 130, 246)),
                Child = new TextBlock
                {
                    Text     = app.GpuLabel,
                    FontSize = 10,
                    FontWeight = Microsoft.UI.Text.FontWeights.Medium
                }
            };

            var headerRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing     = 8,
                Children    =
                {
                    new TextBlock { Text = app.Name, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 13 },
                    modeLabel,
                    gpuBadge
                }
            };

            var pathText = new TextBlock
            {
                Text         = app.CurrentExePath ?? app.SearchPath,
                FontSize     = 11,
                Opacity      = 0.65,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin       = new Thickness(0, 2, 0, 0)
            };

            var itemContent = new StackPanel
            {
                Children = { headerRow, pathText }
            };

            cb.Content = itemContent;
            cb.Checked   += (_, _) => OnCheckChanged();
            cb.Unchecked += (_, _) => OnCheckChanged();

            _rows.Add((cb, app));
            listPanel.Children.Add(cb);
        }

        outer.Children.Add(listPanel);
        scroll.Content = outer;
        Content = scroll;

        PrimaryButtonClick += OnAdd;
        OnCheckChanged();
    }

    private void OnCheckChanged()
    {
        IsPrimaryButtonEnabled = _rows.Any(r => r.cb.IsChecked == true);
    }

    private void OnAdd(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        SelectedApps.Clear();
        foreach (var (cb, app) in _rows)
        {
            if (cb.IsChecked == true)
            {
                SelectedApps.Add(new AppDefinition
                {
                    Name          = app.Name,
                    Category      = "Windows設定からインポート",
                    SearchPath    = app.SearchPath,
                    ExeName       = app.ExeName,
                    SearchMode    = app.SearchMode,
                    Recursive     = app.Recursive,
                    GpuPreference = app.GpuPreference,
                    CurrentExePath = app.CurrentExePath,
                    ManagedPaths  = new List<string>(app.ManagedPaths)
                });
            }
        }
    }
}
