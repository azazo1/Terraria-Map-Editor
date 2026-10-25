using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace TEdit5.Services;

/// <summary>日志级别. 与 Microsoft.Extensions.Logging 的层级保持一致, 便于以后切换.</summary>
public enum LogLevel
{
    Debug,
    Info,
    Warn,
    Error,
}

/// <summary>
/// 轻量文件日志. 按天滚动写入应用数据目录下的 logs 文件夹, 同时输出到 Debug 通道.
/// 日志写入失败时静默降级, 不允许因为日志影响主流程.
/// </summary>
public static class LogManager
{
    private static readonly object Sync = new();
    private static readonly bool VerboseEnabled =
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TEDIT5_DEBUG"));

    private static bool _initialized;

    /// <summary>当天日志文件路径; 初始化失败时为 null.</summary>
    public static string? LogFilePath { get; private set; }

    /// <summary>
    /// 初始化日志文件. 重复调用安全. 应用启动时调用一次即可.
    /// </summary>
    public static void Initialize()
    {
        lock (Sync)
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            try
            {
                LogFilePath = Path.Combine(AppPaths.LogPath, $"tedit5-{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(
                    LogFilePath,
                    $"{Timestamp()} INFO  会话启动, 应用目录 {AppPaths.ApplicationData}{Environment.NewLine}",
                    Encoding.UTF8);
            }
            catch (Exception ex)
            {
                LogFilePath = null;
                System.Diagnostics.Debug.WriteLine($"无法初始化日志文件: {ex.Message}");
            }
        }
    }

    public static void Debug(string message)
    {
        if (!VerboseEnabled)
        {
            return;
        }

        Write(LogLevel.Debug, message, null);
    }

    public static void Info(string message) => Write(LogLevel.Info, message, null);

    public static void Warn(string message) => Write(LogLevel.Warn, message, null);

    public static void Error(string message) => Write(LogLevel.Error, message, null);

    public static void Exception(Exception exception, string? context = null) =>
        Write(LogLevel.Error, context ?? "未处理异常", exception);

    private static void Write(LogLevel level, string message, Exception? exception)
    {
        var line = new StringBuilder()
            .Append(Timestamp())
            .Append(' ')
            .Append(FormatLevel(level))
            .Append("  ")
            .Append(message);

        if (exception != null)
        {
            line.Append(Environment.NewLine).Append(exception);
        }

        var text = line.ToString();
        System.Diagnostics.Debug.WriteLine(text);

        lock (Sync)
        {
            try
            {
                if (LogFilePath == null)
                {
                    return;
                }

                File.AppendAllText(LogFilePath, text + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"写入日志失败: {ex.Message}");
            }
        }
    }

    private static string Timestamp() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

    private static string FormatLevel(LogLevel level) => level switch
    {
        LogLevel.Debug => "DEBUG",
        LogLevel.Info => "INFO ",
        LogLevel.Warn => "WARN ",
        _ => "ERROR",
    };
}
