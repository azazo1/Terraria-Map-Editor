using System.Collections.ObjectModel;
using System.IO;
using TEdit.Editor;
using TEdit.Editor.Clipboard;
using TEdit.Editor.Undo;
using TEdit.Geometry;
using TEdit.Terraria;
using TEdit5.Services;

namespace TEdit5.Services;

/// <summary>
/// 剪贴板缓冲管理. 对应 Windows 版的 ClipboardManager, 但只用核心库的
/// ClipboardBuffer, 不生成预览图 (预览依赖 WPF 位图).
/// </summary>
public partial class ClipboardService : ReactiveObject
{
    private readonly ISelection _selection;

    [Reactive] private ClipboardBuffer? _activeBuffer;
    [Reactive] private bool _instantPaste;

    public ObservableCollection<ClipboardBuffer> Buffers { get; } = new();

    /// <summary>粘贴选项, 面板上的复选框直接绑到这里.</summary>
    public PasteOptions Options { get; } = new();

    public ClipboardService(ISelection selection)
    {
        _selection = selection;
    }

    public void ClearBuffers()
    {
        ActiveBuffer = null;
        Buffers.Clear();
    }

    public void Remove(ClipboardBuffer buffer) => Buffers.Remove(buffer);

    /// <summary>把当前选区复制成新的缓冲.</summary>
    public ClipboardBuffer? CopySelection(World world)
    {
        if (!_selection.IsActive)
        {
            return null;
        }

        var buffer = ClipboardBuffer.GetSelectionBuffer(world, _selection.SelectionArea);
        Buffers.Add(buffer);
        ActiveBuffer = buffer;
        LogManager.Info($"复制选区 {buffer.Size.X}x{buffer.Size.Y} 到剪贴板");
        return buffer;
    }

    /// <summary>把缓冲粘贴到世界里的指定位置.</summary>
    public bool Paste(World world, Vector2Int32 anchor, IUndoManager? undo)
    {
        if (ActiveBuffer == null)
        {
            return false;
        }

        ActiveBuffer.Paste(world, anchor, undo, Options);
        LogManager.Info($"粘贴缓冲 {ActiveBuffer.Size.X}x{ActiveBuffer.Size.Y} 到 {anchor}");
        return true;
    }

    public void Rotate(ClipboardBuffer buffer) => Replace(buffer, buffer.Rotate());

    public void FlipX(ClipboardBuffer buffer) => Replace(buffer, buffer.FlipX());

    public void FlipY(ClipboardBuffer buffer) => Replace(buffer, buffer.FlipY());

    /// <summary>从磁盘导入 schematic.</summary>
    public void Import(string fileName)
    {
        var buffer = ClipboardBuffer.Load(fileName);
        if (buffer == null)
        {
            return;
        }

        Buffers.Add(buffer);
        ActiveBuffer = buffer;
    }

    /// <summary>导出缓冲到 schematic 文件.</summary>
    public void Export(ClipboardBuffer buffer, string fileName, uint version)
    {
        string? directory = Path.GetDirectoryName(fileName);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        buffer.Save(fileName, version);
    }

    private void Replace(ClipboardBuffer oldBuffer, ClipboardBuffer newBuffer)
    {
        int index = Buffers.IndexOf(oldBuffer);
        if (index >= 0)
        {
            Buffers[index] = newBuffer;
        }

        if (ReferenceEquals(ActiveBuffer, oldBuffer))
        {
            ActiveBuffer = newBuffer;
        }
    }
}
