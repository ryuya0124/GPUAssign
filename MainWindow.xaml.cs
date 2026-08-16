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
