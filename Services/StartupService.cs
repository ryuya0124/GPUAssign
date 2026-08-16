using System;
using System.IO;
using Microsoft.Win32;
using System.Reflection;

namespace GPUAssign.Services;

/// <summary>
/// Manages Windows logon auto-start via a Task Scheduler task (preferred)
/// or the Registry Run key as fallback.
/// </summary>
public static class StartupService
{
    private const string TaskName = "GPUAssignAutoSync";
    private const string RunKeyName = "GPUAssign";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>Register the app to run at logon using Task Scheduler via schtasks.exe.</summary>
    public static void EnableStartupTask(string exePath)
    {
        // Use schtasks to create an on-logon task that runs with /silent flag
        var args = $"/Create /F /TN \"{TaskName}\" /TR \"\\\"{exePath}\\\" /silent\" " +
                   "/SC ONLOGON /RL HIGHEST /DELAY 0000:30";

        RunProcess("schtasks.exe", args);
    }

    /// <summary>Remove the logon task.</summary>
    public static void DisableStartupTask()
    {
        try { RunProcess("schtasks.exe", $"/Delete /TN \"{TaskName}\" /F"); }
        catch { /* task might not exist */ }
    }

    public static bool IsStartupEnabled()
    {
        try
        {
            var result = RunProcessWithOutput("schtasks.exe", $"/Query /TN \"{TaskName}\"");
            return result.Contains(TaskName, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    // ---- helpers ----

    private static void RunProcess(string exe, string args)
    {
        using var p = new System.Diagnostics.Process();
        p.StartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName  = exe,
            Arguments = args,
            UseShellExecute        = false,
            CreateNoWindow         = true,
            RedirectStandardOutput = true,
            RedirectStandardError  = true
        };
        p.Start();
        p.WaitForExit(10_000);
    }

    private static string RunProcessWithOutput(string exe, string args)
    {
        using var p = new System.Diagnostics.Process();
        p.StartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName  = exe,
            Arguments = args,
            UseShellExecute        = false,
            CreateNoWindow         = true,
            RedirectStandardOutput = true,
            RedirectStandardError  = true
        };
        p.Start();
        var output = p.StandardOutput.ReadToEnd();
        p.WaitForExit(10_000);
        return output;
    }
}
