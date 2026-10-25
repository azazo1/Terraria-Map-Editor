using System;
using System.Diagnostics;
using System.IO;

namespace TEdit5.Services;

/// <summary>
/// 应用数据目录约定. 用户级数据统一收在系统应用数据目录下, 避免污染系统临时目录.
/// </summary>
public static class AppPaths
{
    private const string FolderName = "TEdit5";

    /// <summary>应用数据根目录.</summary>
    public static string ApplicationData { get; } = ResolveApplicationData();

    /// <summary>运行时缓存根目录, 撤销缓存与预览缓存都放在这里.</summary>
    public static string TempPath { get; } = EnsureDirectory(Path.Combine(ApplicationData, "temp"));

    /// <summary>撤销与重做临时文件目录.</summary>
    public static string UndoPath { get; } = EnsureDirectory(Path.Combine(TempPath, "undo"));

    /// <summary>日志目录.</summary>
    public static string LogPath { get; } = EnsureDirectory(Path.Combine(ApplicationData, "logs"));

    /// <summary>世界备份目录.</summary>
    public static string BackupPath { get; } = EnsureDirectory(Path.Combine(ApplicationData, "backups"));

    /// <summary>自动保存目录.</summary>
    public static string AutoSavePath { get; } = EnsureDirectory(Path.Combine(ApplicationData, "autosave"));

    private static string ResolveApplicationData()
    {
        if (OperatingSystem.IsMacOS())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "Application Support", FolderName);
        }

        if (OperatingSystem.IsWindows())
        {
            var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(roaming, FolderName);
        }

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(local, FolderName);
    }

    private static string EnsureDirectory(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
        }
        catch (Exception ex)
        {
            // 这里不能调用 LogManager, 否则会在日志目录创建失败时形成递归.
            Debug.WriteLine($"无法创建目录 {path}: {ex.Message}");
        }

        return path;
    }
}
