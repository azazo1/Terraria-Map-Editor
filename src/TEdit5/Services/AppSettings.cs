using System;
using System.IO;
using System.Text.Json;

namespace TEdit5.Services;

/// <summary>
/// 用户级设置. 目前只有纹理目录一项, 保存在应用数据目录下的 settings.json.
/// </summary>
public class AppSettings
{
    private const string FileName = "settings.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    private static AppSettings? _current;

    /// <summary>物品与方块图标的存放目录.</summary>
    public string TexturePath { get; set; } = string.Empty;

    /// <summary>设置文件路径.</summary>
    public static string FilePath => Path.Combine(AppPaths.ApplicationData, FileName);

    /// <summary>读取设置, 文件不存在时返回默认值.</summary>
    public static AppSettings Load()
    {
        if (_current != null)
        {
            return _current;
        }

        AppSettings settings;
        try
        {
            string path = FilePath;
            settings = File.Exists(path)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Options) ?? new AppSettings()
                : new AppSettings();
        }
        catch (Exception)
        {
            settings = new AppSettings();
        }

        if (string.IsNullOrWhiteSpace(settings.TexturePath))
        {
            settings.TexturePath = Path.Combine(AppPaths.ApplicationData, "Textures");
        }

        _current = settings;
        return settings;
    }

    /// <summary>把当前设置写回文件.</summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.ApplicationData);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Options));
        }
        catch (Exception ex)
        {
            LogManager.Error($"保存设置失败: {ex.Message}");
        }
    }
}
