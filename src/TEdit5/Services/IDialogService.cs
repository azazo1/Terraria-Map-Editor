using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace TEdit5.Services;

/// <summary>文件对话框与简单提示对话框. 视图模型只依赖这个接口, 不直接碰窗口.</summary>
public interface IDialogService
{
    /// <summary>绑定当前顶层窗口, 由主窗口在打开时调用一次.</summary>
    void AttachTo(TopLevel topLevel);

    /// <summary>选择要打开的世界文件, 取消时返回 null.</summary>
    Task<string?> PickWorldToOpenAsync();

    /// <summary>选择要打开的玩家存档.</summary>
    Task<string?> PickPlayerToOpenAsync();

    /// <summary>选择玩家存档的保存位置.</summary>
    Task<string?> PickPlayerToSaveAsync(string suggestedFileName);

    /// <summary>选择另存为的路径, 取消时返回 null.</summary>
    Task<string?> PickWorldToSaveAsync(string suggestedFileName);
}

public sealed class DialogService : IDialogService
{
    private static readonly FilePickerFileType WorldFileType = new("Terraria 世界文件")
    {
        Patterns = new[] { "*.wld", "*.twld" },
    };

    private TopLevel? _topLevel;

    public void AttachTo(TopLevel topLevel) => _topLevel = topLevel;

    public async Task<string?> PickPlayerToOpenAsync()
    {
        if (_topLevel == null)
        {
            LogManager.Warn("文件对话框不可用: 尚未绑定顶层窗口.");
            return null;
        }

        var files = await _topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "打开玩家存档",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("玩家存档")
                {
                    Patterns = new[] { "*.plr" },
                },
            },
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickPlayerToSaveAsync(string suggestedFileName)
    {
        if (_topLevel == null)
        {
            LogManager.Warn("文件对话框不可用: 尚未绑定顶层窗口.");
            return null;
        }

        var file = await _topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "保存玩家存档",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "plr",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("玩家存档")
                {
                    Patterns = new[] { "*.plr" },
                },
            },
        });

        return file?.TryGetLocalPath();
    }

    public async Task<string?> PickWorldToOpenAsync()
    {
        if (_topLevel == null)
        {
            LogManager.Warn("文件对话框不可用: 尚未绑定顶层窗口.");
            return null;
        }

        var files = await _topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "打开世界",
            AllowMultiple = false,
            FileTypeFilter = new[] { WorldFileType },
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickWorldToSaveAsync(string suggestedFileName)
    {
        if (_topLevel == null)
        {
            LogManager.Warn("文件对话框不可用: 尚未绑定顶层窗口.");
            return null;
        }

        var file = await _topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "保存世界",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "wld",
            FileTypeChoices = new[] { WorldFileType },
            ShowOverwritePrompt = true,
        });

        return file?.TryGetLocalPath();
    }
}
