using GPUAssign.Pages;
using GPUAssign.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GPUAssign;

public sealed partial class MainWindow : Window
{
    public static new MainWindow? Current { get; private set; }

    public MainWindow()
    {
        Current = this;
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        if (Content is FrameworkElement root)
        {
            void UpdateCaptionColors()
            {
                AppWindow.TitleBar.ButtonForegroundColor = root.ActualTheme == ElementTheme.Dark ? Microsoft.UI.Colors.White : Microsoft.UI.Colors.Black;
                AppWindow.TitleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(255, 128, 128, 128);
                AppWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
            }
            root.ActualThemeChanged += (_, _) => UpdateCaptionColors();
            root.Loaded += (_, _) =>
            {
                UpdateCaptionColors();
                double scale = root.XamlRoot.RasterizationScale;
                var workArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(AppWindow.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Primary).WorkArea;
                AppWindow.Resize(new Windows.Graphics.SizeInt32(System.Math.Min(workArea.Width, (int)(1100 * scale)), System.Math.Min(workArea.Height, (int)(800 * scale))));
            };
        }

        UpdateLocalizedStrings();
        NavFrame.Navigate(typeof(AppsPage));
    }

    public void UpdateLocalizedStrings()
    {
        Title = L.Get("app.title");
        AppTitleBar.Title = L.Get("app.title");

        NavApps.Content    = L.Get("nav.apps");
        NavSync.Content    = L.Get("nav.syncLog");
        NavBackup.Content  = L.Get("nav.backup");
        NavSettings.Content = L.Get("nav.settings");
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item) return;

        var tag = item.Tag as string;
        var pageType = tag switch
        {
            "apps"     => typeof(AppsPage),
            "sync"     => typeof(SyncLogPage),
            "backup"   => typeof(BackupPage),
            "settings" => typeof(SettingsPage),
            _          => typeof(AppsPage)
        };

        NavFrame.Navigate(pageType);
    }

    private void TitleBar_BackRequested(TitleBar sender, object args)
    {
        if (NavFrame.CanGoBack)
            NavFrame.GoBack();
    }

    private void TitleBar_PaneToggleRequested(TitleBar sender, object args)
    {
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
    }
}
