using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

    private readonly List<(CheckBox cb, AppDefinition app, Image img)> _rows = new();

    public ImportExistingDialog(List<AppDefinition> detectedApps)
    {
        Title             = L.Get("dialog.import.title");
        PrimaryButtonText = L.Get("action.importSelected");
        CloseButtonText   = L.Get("action.cancel");
        DefaultButton     = ContentDialogButton.Primary;
        IsPrimaryButtonEnabled = detectedApps.Count > 0;

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = 520
        };

        var outer = new StackPanel { Width = 500, Spacing = 14 };

        outer.Children.Add(new TextBlock
        {
            Text         = L.Get("dialog.import.desc"),
            TextWrapping = TextWrapping.Wrap,
            Opacity      = 0.8,
            FontSize     = 12
        });

        var selectAllRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        var selectAllBtn = new Button { Content = L.Get("action.selectAll"), FontSize = 12 };
        var unselectAllBtn = new Button { Content = L.Get("action.deselectAll"), FontSize = 12 };

        selectAllBtn.Click += (_, _) =>
        {
            foreach (var (cb, _, _) in _rows) cb.IsChecked = true;
            OnCheckChanged();
        };
        unselectAllBtn.Click += (_, _) =>
        {
            foreach (var (cb, _, _) in _rows) cb.IsChecked = false;
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

            var img = new Image
            {
                Width = 24,
                Height = 24,
                Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center
            };

            var letterBorder = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(4),
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 34, 130, 246)),
                Child = new TextBlock
                {
                    Text = app.NameInitial,
                    FontSize = 12,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };

            var iconGrid = new Grid { Width = 24, Height = 24 };
            iconGrid.Children.Add(letterBorder);
            iconGrid.Children.Add(img);

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
                    iconGrid,
                    new TextBlock { Text = app.Name, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 13, VerticalAlignment = VerticalAlignment.Center },
                    modeLabel,
                    gpuBadge
                }
            };

            var displayPath = app.IsStoreApp
                ? (string.IsNullOrEmpty(app.ExeName) ? L.Get("searchMode.storeApp") : app.ExeName)
                : (app.CurrentExePath ?? app.SearchPath);

            var pathText = new TextBlock
            {
                Text         = displayPath,
                FontSize     = 11,
                Opacity      = 0.65,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin       = new Thickness(32, 2, 0, 0)
            };

            var itemContent = new StackPanel
            {
                Children = { headerRow, pathText }
            };

            cb.Content = itemContent;
            cb.Checked   += (_, _) => OnCheckChanged();
            cb.Unchecked += (_, _) => OnCheckChanged();

            _rows.Add((cb, app, img));
            listPanel.Children.Add(cb);
        }

        outer.Children.Add(listPanel);
        scroll.Content = outer;
        Content = scroll;

        PrimaryButtonClick += OnAdd;
        OnCheckChanged();

        // Load icons asynchronously
        _ = LoadDialogIconsAsync();
    }

    private async Task LoadDialogIconsAsync()
    {
        foreach (var (_, app, img) in _rows)
        {
            var target = app.IsStoreApp ? app.ExeName : (app.CurrentExePath ?? app.SearchPath);
            if (!string.IsNullOrEmpty(target))
            {
                var icon = await IconService.GetAppIconAsync(target);
                if (icon != null)
                {
                    img.Source = icon;
                    app.IconSource = icon;
                }
            }
        }
    }

    private void OnCheckChanged()
    {
        IsPrimaryButtonEnabled = _rows.Any(r => r.cb.IsChecked == true);
    }

    private void OnAdd(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        SelectedApps.Clear();
        foreach (var (cb, app, _) in _rows)
        {
            if (cb.IsChecked == true)
            {
                SelectedApps.Add(new AppDefinition
                {
                    Name          = app.Name,
                    Category      = string.IsNullOrEmpty(app.Category) ? L.Get("category.imported") : app.Category,
                    SearchPath    = app.SearchPath,
                    ExeName       = app.ExeName,
                    SearchMode    = app.SearchMode,
                    Recursive     = app.Recursive,
                    GpuPreference = app.GpuPreference,
                    CurrentExePath = app.CurrentExePath,
                    IconSource    = app.IconSource,
                    ManagedPaths  = new List<string>(app.ManagedPaths)
                });
            }
        }
    }
}
