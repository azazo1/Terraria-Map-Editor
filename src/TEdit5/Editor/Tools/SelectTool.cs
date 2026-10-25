using Avalonia;
using Avalonia.Input;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Media;
using TEdit5.Controls;
using TEdit.Editor;

namespace TEdit5.Editor.Tools;

/// <summary>选择工具: 在世界视图上拉出矩形区域, 区域由渲染控件负责显示.</summary>
public partial class SelectTool : ReactiveObject, IMouseTool
{
    public ICustomDrawOperation? DrawTool { get; }

    public string Name { get; } = "Select";

    public string Tooltip { get; } = "选择区域";

    [Reactive] private bool _isActive;

    public string IconName { get; } = "mdi-select";

    /// <summary>原版工具栏使用的位图图标, 取自仓库 docs/Icons.</summary>
    public Avalonia.Media.Imaging.Bitmap? IconBitmap { get; } = AppIconAssets.Load("shape_square.png");

    public bool HasBitmapIcon => IconBitmap != null;

    public Geometry Icon { get; } = AppIcons.Get("IconSquareMultiple24");
}
