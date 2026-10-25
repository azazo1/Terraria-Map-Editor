using Avalonia;
using Avalonia.Input;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Media;
using TEdit5.Controls;
using TEdit.Editor;

namespace TEdit5.Editor.Tools;

/// <summary>箭头工具: 只用来查看和拖动画布, 不做任何编辑.</summary>
public partial class ArrowTool : ReactiveObject, IMouseTool
{
    public ICustomDrawOperation? DrawTool { get; }

    public string Name { get; } = "Arrow";

    public string Tooltip { get; } = "箭头 (查看)";

    [Reactive] private bool _isActive;

    public string IconName { get; } = "mdi-cursor-default";

    /// <summary>原版工具栏使用的位图图标, 取自仓库 docs/Icons.</summary>
    public Avalonia.Media.Imaging.Bitmap? IconBitmap { get; } = AppIconAssets.Load("cursor.png");

    public bool HasBitmapIcon => IconBitmap != null;

    public Geometry Icon { get; } = AppIcons.Get("IconCursor24");
}
