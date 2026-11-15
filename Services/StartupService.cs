using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace GPUAssign.Services;

/// <summary>Current-user startup; syncing HKCU never needs administrator privileges.</summary>
public static class StartupService
{
    private const string TaskName = "GPUAssignAutoSync";
    private const string RunKeyName = "GPUAssign";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static void EnableStartupTask(string exePath)
    {
        if (!Path.IsPathFullyQualified(exePath) || !File.Exists(exePath))
            throw new FileNotFoundException("GPU Assign executable was not found.", exePath);

        var result = RunTask("/Create", "/F", "/TN", TaskName, "/TR", $"\"{exePath}\" /silent",
            "/SC", "ONLOGON", "/RL", "LIMITED", "/DELAY", "0000:30");
        if (result.ExitCode == 0)
        {
            using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            runKey?.DeleteValue(RunKeyName, false);
            return;
        }

        // A standard user may not create logon triggers. Avoid duplicate startup.
        if (HasScheduledTask()) throw new InvalidOperationException(result.Error);
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        key.SetValue(RunKeyName, $"\"{exePath}\" /silent");
    }

    public static void DisableStartupTask()
    {
        if (HasScheduledTask())
        {
            var result = RunTask("/Delete", "/TN", TaskName, "/F");
            if (result.ExitCode != 0) throw new InvalidOperationException(result.Error);
        }
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
        key?.DeleteValue(RunKeyName, false);
    }

    public static bool IsStartupEnabled()
    {
        if (HasScheduledTask()) return true;
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(RunKeyName) is string value && !string.IsNullOrWhiteSpace(value);
    }

    private static bool HasScheduledTask() => RunTask("/Query", "/TN", TaskName).ExitCode == 0;

    private static (int ExitCode, string Output, string Error) RunTask(params string[] args)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, "schtasks.exe"),
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            }
        };
        foreach (var arg in args) process.StartInfo.ArgumentList.Add(arg);
        process.Start();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(10_000))
        {
            process.Kill();
            throw new TimeoutException("Task Scheduler did not respond within 10 seconds.");
        }
        return (process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
    }
}
