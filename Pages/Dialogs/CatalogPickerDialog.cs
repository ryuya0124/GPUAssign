using System;
using System.Collections.Generic;
using System.Linq;
using GPUAssign.Models;
using GPUAssign.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GPUAssign.Pages.Dialogs;

/// <summary>
/// Shows the built-in app catalog so users can bulk-select entries
/// to add to their personal list.
/// Built entirely in C# (no XAML) to avoid WMC9999.
/// </summary>
public sealed class CatalogPickerDialog : ContentDialog
{
    /// <summary>Apps the user checked to add.</summary>
    public List<AppDefinition> SelectedApps { get; } = new();

    private readonly List<(CheckBox cb, AppDefinition app)> _rows = new();

    public CatalogPickerDialog(List<AppDefinition> catalog)
    {
        Title             = L.Get("dialog.addFromCatalog.title");
        PrimaryButtonText = L.Get("action.add");
        CloseButtonText   = L.Get("action.cancel");
        DefaultButton     = ContentDialogButton.Primary;
        IsPrimaryButtonEnabled = false;

        // Group by category
        var grouped = catalog
            .GroupBy(a => a.Category)
            .OrderBy(g => g.Key);

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = 500
        };

        var outer = new StackPanel { Width = 440, Spacing = 16 };

        outer.Children.Add(new TextBlock
        {
            Text         = L.Get("dialog.addFromCatalog.subtitle"),
            TextWrapping = TextWrapping.Wrap,
            Opacity      = 0.7,
            FontSize     = 13
        });

        foreach (var group in grouped)
        {
            // Category header
            outer.Children.Add(new TextBlock
            {
                Text       = group.Key,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize   = 13,
                Margin     = new Thickness(0, 4, 0, 0)
            });

            var categoryPanel = new StackPanel { Spacing = 6, Margin = new Thickness(8, 0, 0, 0) };

            foreach (var app in group)
            {
                var cb = new CheckBox { IsChecked = false };

                // Inner layout: name + search mode badge
                var modeLabel = new Border
                {
                    CornerRadius    = new CornerRadius(4),
                    Padding         = new Thickness(6, 2, 6, 2),
                    Background      = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Windows.UI.Color.FromArgb(40, 128, 128, 255)),
                    Child = new TextBlock
                    {
                        Text     = app.SearchModeLabel,
                        FontSize = 10,
                        Opacity  = 0.8
                    }
                };

                var row = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing     = 8,
                    Children    =
                    {
                        new TextBlock { Text = app.Name, VerticalAlignment = VerticalAlignment.Center },
                        modeLabel
                    }
                };

                cb.Content = row;

                cb.Checked   += (_, _) => OnCheckChanged();
                cb.Unchecked += (_, _) => OnCheckChanged();

                _rows.Add((cb, app));
                categoryPanel.Children.Add(cb);
            }

            outer.Children.Add(categoryPanel);
        }

        scroll.Content = outer;
        Content = scroll;

        PrimaryButtonClick += OnAdd;
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
                // Clone so each user entry gets a fresh Id
                SelectedApps.Add(new AppDefinition
                {
                    Name         = app.Name,
                    Category     = app.Category,
                    SearchPath   = app.SearchPath,
                    ExeName      = app.ExeName,
                    SearchMode   = app.SearchMode,
                    Recursive    = app.Recursive,
                    GpuPreference = app.GpuPreference
                });
            }
        }
    }
}
