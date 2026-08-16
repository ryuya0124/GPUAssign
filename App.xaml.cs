using System;
using System.Linq;
using GPUAssign.Models;
using GPUAssign.Services;
using Microsoft.UI.Xaml;

namespace GPUAssign;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// Supports /silent argument for headless auto-sync (used by Task Scheduler).
    /// </summary>
    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        var config = ConfigService.Load();

        // Initialize localization
        LocalizationService.Initialize(config.Language);

        var cmdArgs = Environment.GetCommandLineArgs();
        bool silent = cmdArgs.Any(a => a.Equals("/silent", StringComparison.OrdinalIgnoreCase));

        if (silent)
        {
            // Headless mode: sync in background and exit without showing UI
            try
            {
                try { BackupService.CreateBackup(); } catch { /* non-fatal */ }

                var results = await SyncService.SyncAllAsync(config);

                foreach (var r in results)
                {
                    Pages.SyncLogPage.AppendLog(
                        r.AppName,
                        r.ErrorMessage is null,
                        r.ErrorMessage ?? (r.Changed ? $"更新 → {r.NewPath}" : "変更なし"));
                }
            }
            catch { /* silent, best-effort */ }

            Exit();
            return;
        }

        var mainWindow = new MainWindow();
        _window = mainWindow;

        // Apply Theme preference
        ApplyTheme(mainWindow.Content as FrameworkElement, config.Theme);

        _window.Activate();
    }

    public static void ApplyTheme(FrameworkElement? root, string theme)
    {
        if (root == null) return;
        root.RequestedTheme = theme switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
    }
}
