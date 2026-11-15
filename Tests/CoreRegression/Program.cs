using GPUAssign.Models;
using GPUAssign.Services;
using Microsoft.Win32;

if (args.Contains("--damaged-config"))
{
    try { ConfigService.Load(); return 2; }
    catch (System.Text.Json.JsonException) { return 0; }
}

int failed = 0, passed = 0;
void Test(string name, Action action)
{
    try { action(); passed++; Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failed++; Console.WriteLine($"FAIL {name}: {ex.Message}"); }
}
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

string root = Path.Combine(Path.GetTempPath(), "GPUAssign-regression-" + Guid.NewGuid());
Directory.CreateDirectory(root);
const string regPath = @"Software\Microsoft\DirectX\UserGpuPreferences";
string testExe = Path.Combine(root, "fixture.exe");
string quotedExe = Path.Combine(root, "quoted.exe");
Dictionary<string, string?> ReadRegistry()
{
    using var key = Registry.CurrentUser.OpenSubKey(regPath);
    return key?.GetValueNames().ToDictionary(name => name, name => key.GetValue(name)?.ToString()) ?? new();
}
var registryBefore = ReadRegistry();
try
{
    foreach (string version in new[] { "1.2.9", "1.10.0" })
    {
        Directory.CreateDirectory(Path.Combine(root, "app-" + version));
        File.WriteAllText(Path.Combine(root, "app-" + version, "fixture.exe"), "fixture");
    }
    Test("glob keeps absolute drive root", () =>
    {
        var app = new AppDefinition { SearchMode = SearchMode.Glob, SearchPath = Path.Combine(root, "app-*"), ExeName = "fixture.exe" };
        string expected = Path.Combine(root, "app-1.10.0", "fixture.exe");
        Check(ExeDiscoveryService.FindBestMatch(app) == expected, "newest absolute glob path missing");
    });
    Test("numeric version ordering", () =>
    {
        var app = new AppDefinition { SearchPath = root, ExeName = "fixture.exe", Recursive = true };
        Check(ExeDiscoveryService.FindBestMatch(app) == Path.Combine(root, "app-1.10.0", "fixture.exe"), "1.10 must outrank 1.2");
    });
    Test("regex rejects catastrophic backtracking in bounded time", () =>
    {
        File.WriteAllText(Path.Combine(root, new string('a', 180) + "!.exe"), "fixture");
        var task = Task.Run(() => ExeDiscoveryService.FindAllMatches(new AppDefinition { SearchPath = root, ExeName = "^(a+)+$", SearchMode = SearchMode.Regex }));
        Check(task.Wait(TimeSpan.FromSeconds(3)), "regex did not finish within 3 seconds");
        Check(task.Result.Count == 0, "unexpected regex match");
    });
    Test("GPU update preserves other Windows graphics settings", () =>
    {
        using var key = Registry.CurrentUser.CreateSubKey(regPath);
        key.SetValue(testExe, "SwapEffectUpgradeEnable=1;GpuPreference=1;AutoHDREnable=0;");
        GpuPreferenceService.SetPreference(testExe, GpuPreference.HighPerformance);
        string raw = (string)key.GetValue(testExe)!;
        Check(raw.Contains("SwapEffectUpgradeEnable=1;") && raw.Contains("AutoHDREnable=0;"), "other graphics flags lost");
        Check(GpuPreferenceService.GetPreference(testExe) == GpuPreference.HighPerformance, "GPU value not updated");
    });
    Test("preference parser rejects embedded and out-of-range fields", () =>
    {
        using var key = Registry.CurrentUser.CreateSubKey(regPath);
        foreach (string raw in new[] { "GpuPreference=12;", "OtherGpuPreference=2;", "GpuPreference=9;" })
        {
            key.SetValue(testExe, raw);
            Check(GpuPreferenceService.GetPreference(testExe) is null, "accepted invalid field: " + raw);
        }
    });
    Test("registry backup round trips quotes and backslashes", () =>
    {
        using var key = Registry.CurrentUser.CreateSubKey(regPath);
        string expected = "GpuPreference=1;Note=\"C:\\temp\\\";";
        key.SetValue(quotedExe, expected);
        string exported = GpuPreferenceService.ExportToReg();
        // Import only the fixture value; never replay unrelated user preferences.
        string fixture = exported.Split('\n').Single(line => line.Contains("quoted.exe"));
        Check(fixture.Contains("Note=\\\""), "exported .reg value must escape double quotes");
        key.DeleteValue(quotedExe);
        GpuPreferenceService.ImportFromReg("Windows Registry Editor Version 5.00\n\n[HKEY_CURRENT_USER\\" + regPath + "]\n" + fixture);
        Check((string?)key.GetValue(quotedExe) == expected, "quoted value changed during restore");
    });
    Test("sync isolates invalid discovery from healthy entries", () =>
    {
        File.WriteAllText(testExe, "fixture");
        var bad = new AppDefinition { SearchPath = root, ExeName = "bad\0pattern", SearchMode = SearchMode.LatestVersion };
        var good = new AppDefinition { SearchPath = root, ExeName = "fixture.exe", SearchMode = SearchMode.Fixed, GpuPreference = GpuPreference.PowerSaving };
        var config = ConfigService.Load();
        config.Apps = new() { bad, good };
        var results = SyncService.SyncAllAsync(config).GetAwaiter().GetResult();
        Check(results.Count == 2 && results[0].ErrorMessage != null && results[1].ErrorMessage == null, "one invalid app aborted the entire sync");
    });
    Test("settings pages share current config", () =>
    {
        var appsPage = ConfigService.Load();
        var settingsPage = ConfigService.Load();
        appsPage.Theme = "Dark";
        ConfigService.Save(appsPage);
        settingsPage.Language = "en-US";
        ConfigService.Save(settingsPage);
        using var persisted = System.Text.Json.JsonDocument.Parse(File.ReadAllText(ConfigService.ConfigFilePath));
        Check(persisted.RootElement.GetProperty("theme").GetString() == "Dark", "stale settings overwrote a newer change");
    });
    Test("failed config write preserves existing file", () =>
    {
        string saved = File.ReadAllText(ConfigService.ConfigFilePath);
        File.SetAttributes(ConfigService.ConfigFilePath, FileAttributes.ReadOnly);
        bool failedWrite = false;
        try { ConfigService.Save(ConfigService.Load()); }
        catch (UnauthorizedAccessException) { failedWrite = true; }
        finally { File.SetAttributes(ConfigService.ConfigFilePath, FileAttributes.Normal); }
        Check(failedWrite && File.ReadAllText(ConfigService.ConfigFilePath) == saved, "failed write was swallowed or damaged configuration");
    });
    Test("damaged configuration is not silently replaced", () =>
    {
        string saved = File.ReadAllText(ConfigService.ConfigFilePath);
        const string damaged = "{invalid configuration";
        try
        {
            File.WriteAllText(ConfigService.ConfigFilePath, damaged);
            using var child = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!, "--damaged-config") { UseShellExecute = false, CreateNoWindow = true })!;
            Check(child.WaitForExit(5000) && child.ExitCode == 0, "damaged config was accepted");
            Check(File.ReadAllText(ConfigService.ConfigFilePath) == damaged, "damaged personal settings were overwritten");
        }
        finally { File.WriteAllText(ConfigService.ConfigFilePath, saved); }
    });
}
finally
{
    using var key = Registry.CurrentUser.OpenSubKey(regPath, true);
    key?.DeleteValue(testExe, false);
    key?.DeleteValue(quotedExe, false);
    // root is a unique temporary directory created by this test.
    Directory.Delete(root, true);
}
Test("unrelated Windows GPU settings remain unchanged", () =>
{
    var after = ReadRegistry();
    Check(after.Count == registryBefore.Count && registryBefore.All(pair => after.TryGetValue(pair.Key, out var value) && value == pair.Value), "an unrelated registry value changed");
});
Console.WriteLine($"{passed} passed, {failed} failed");
return failed == 0 ? 0 : 1;
