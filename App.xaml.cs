using System;
using System.Linq;
using GPUAssign.Models;
using GPUAssign.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Runtime.InteropServices;

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
        UnhandledException += async (_, e) =>
        {
            e.Handled = true;
            if (_window?.Content is FrameworkElement root && root.XamlRoot != null)
            {
                try
                {
                    await new ContentDialog
                    {
                        Title = L.Get("status.error"), Content = e.Exception.Message,
                        CloseButtonText = L.Get("action.close"), XamlRoot = root.XamlRoot
                    }.ShowAsync();
                }
                catch { ShowStartupError(e.Exception.Message); }
            }
            else ShowStartupError(e.Exception.Message);
        };
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// Supports /silent argument for headless auto-sync (used by Task Scheduler).
    /// </summary>
    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        AppConfig config;
        try { config = ConfigService.Load(); }
        catch (Exception ex)
        {
            // Never replace a damaged or unreadable personal configuration.
            if (!Environment.GetCommandLineArgs().Any(a => a.Equals("/silent", StringComparison.OrdinalIgnoreCase)))
                ShowStartupError($"{ConfigService.ConfigFilePath}\n\n{ex.Message}");
            Environment.ExitCode = 1;
            Exit();
            return;
        }

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
            catch { Environment.ExitCode = 1; }

            Exit();
            return;
        }

        var mainWindow = new MainWindow();
        _window = mainWindow;

        // Apply Theme preference
        ApplyTheme(mainWindow.Content as FrameworkElement, config.Theme);

        _window.Activate();
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr owner, string text, string caption, uint type);

    private static void ShowStartupError(string message) => MessageBox(IntPtr.Zero, message, "GPU Assign", 0x10);

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
