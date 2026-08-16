using System;
using System.IO;

namespace GPUAssign.Services;

/// <summary>
/// Manages backup and restore of the Windows GPU preference registry key.
/// Backups are stored as .reg files in the portable data folder (data/backups/).
/// </summary>
public static class BackupService
{
    public static string BackupDir => Path.Combine(ConfigService.ConfigDir, "backups");

    public static string CreateBackup()
    {
        Directory.CreateDirectory(BackupDir);

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var filePath  = Path.Combine(BackupDir, $"{timestamp}.reg");

        var content = GpuPreferenceService.ExportToReg();
        File.WriteAllText(filePath, content, System.Text.Encoding.Unicode);

        return filePath;
    }

    public static void RestoreBackup(string regFilePath)
    {
        if (!File.Exists(regFilePath))
            throw new FileNotFoundException($"バックアップファイルが見つかりません: {regFilePath}");

        var content = File.ReadAllText(regFilePath, System.Text.Encoding.Unicode);
        GpuPreferenceService.ImportFromReg(content);
    }

    public static string[] GetBackups()
    {
        if (!Directory.Exists(BackupDir))
            return Array.Empty<string>();

        return Directory.GetFiles(BackupDir, "*.reg")
            .OrderByDescending(f => f)
            .ToArray();
    }

    public static void DeleteBackup(string regFilePath)
    {
        if (File.Exists(regFilePath))
            File.Delete(regFilePath);
    }
}
