using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TEdit.Common;
using TEdit5.Services;

namespace TEdit5.Utility;

/// <summary>缓存目录的统计结果.</summary>
public readonly record struct CacheStats(int FileCount, long Bytes);

/// <summary>缓存清理结果.</summary>
public readonly record struct CacheCleanupResult(int DeletedFileCount, long FreedBytes, int FailedFileCount)
{
    public static CacheCleanupResult operator +(CacheCleanupResult left, CacheCleanupResult right) =>
        new(
            left.DeletedFileCount + right.DeletedFileCount,
            left.FreedBytes + right.FreedBytes,
            left.FailedFileCount + right.FailedFileCount);
}

/// <summary>
/// 缓存与备份文件的维护操作. 当前只保留撤销缓存需要的部分, 后续移植备份与自动保存时再扩展.
/// </summary>
public static class FileMaintenance
{
    /// <summary>剥掉备份后缀 (.tedit, .bak, .autosave, .tmp), 还原出世界文件路径.</summary>
    public static string NormalizeWorldFilePath(string filename) =>
        FilePathUtility.NormalizeWorldFilePath(filename);

    /// <summary>是否为备份文件.</summary>
    public static bool IsBackupFile(string filename) =>
        FilePathUtility.IsBackupFile(filename);

    /// <summary>把字节数格式化成便于阅读的形式.</summary>
    public static string FormatFileSize(long bytes) =>
        FilePathUtility.FormatFileSize(bytes);

    /// <summary>
    /// 清空目录内容, 保留目录本身. excludedFiles 中的绝对路径不会被删除.
    /// </summary>
    public static CacheCleanupResult ClearDirectoryContents(
        string directory,
        IReadOnlySet<string>? excludedFiles = null)
    {
        if (!Directory.Exists(directory))
        {
            return default;
        }

        HashSet<string>? exclusions = excludedFiles == null
            ? null
            : new HashSet<string>(excludedFiles.Select(Path.GetFullPath), StringComparer.OrdinalIgnoreCase);

        CacheCleanupResult result = default;

        foreach (var file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
        {
            if (exclusions?.Contains(Path.GetFullPath(file)) == true)
            {
                continue;
            }

            result += DeleteCacheFile(file);
        }

        foreach (var child in Directory.GetDirectories(directory, "*", SearchOption.AllDirectories)
                     .OrderByDescending(path => path.Length))
        {
            try
            {
                if (!Directory.EnumerateFileSystemEntries(child).Any())
                {
                    Directory.Delete(child, false);
                }
            }
            catch (Exception ex)
            {
                LogManager.Warn($"无法移除空缓存目录 {child}: {ex.Message}");
            }
        }

        return result;
    }

    /// <summary>统计一组目录占用的文件数与字节数.</summary>
    public static CacheStats GetCacheStats(IEnumerable<string> directories)
    {
        int fileCount = 0;
        long bytes = 0;

        foreach (var path in directories.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(path))
            {
                continue;
            }

            try
            {
                foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        fileCount++;
                        bytes += new FileInfo(file).Length;
                    }
                    catch (IOException)
                    {
                        // 统计期间文件可能被删除, 忽略.
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Warn($"无法检查缓存目录 {path}: {ex.Message}");
            }
        }

        return new CacheStats(fileCount, bytes);
    }

    private static CacheCleanupResult DeleteCacheFile(string file)
    {
        try
        {
            long size = new FileInfo(file).Length;
            File.Delete(file);
            return new CacheCleanupResult(1, size, 0);
        }
        catch (FileNotFoundException)
        {
            return default;
        }
        catch (Exception ex)
        {
            LogManager.Warn($"无法删除缓存文件 {file}: {ex.Message}");
            return new CacheCleanupResult(0, 0, 1);
        }
    }
}
