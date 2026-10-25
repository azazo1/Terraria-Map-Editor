using Avalonia;
using Avalonia.Input;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Media;
using TEdit5.Controls;
using TEdit.Editor;
using TEdit.Geometry;

namespace TEdit5.Editor.Tools;

/// <summary>
/// 刷子工具: 按画笔形状成片涂抹. 左键涂抹, 右键擦除, 沿用画笔设置里的尺寸与形状.
/// </summary>
public partial class BrushTool : ReactiveObject, IMouseTool
{
    public ICustomDrawOperation? DrawTool { get; }

    public string Name { get; } = "Brush";

    public string Tooltip { get; } = "刷子 (左键涂抹, 右键擦除)";

    [Reactive] private bool _isActive;

    public string IconName { get; } = "mdi-brush";

    /// <summary>原版工具栏使用的位图图标, 取自仓库 docs/Icons.</summary>
    public Avalonia.Media.Imaging.Bitmap? IconBitmap { get; } = AppIconAssets.Load("paintbrush.png");

    public bool HasBitmapIcon => IconBitmap != null;

    public Geometry Icon { get; } = AppIcons.Get("IconBroom24");

    private WorldEditor? _editor;
    private bool _isPainting;

    public void Press(WorldEditor editor, PointerPoint buttons, Point worldCoordinate)
    {
        _editor = editor;
        _editor.BeginOperationAsync().Wait();
        _isPainting = true;
        Paint(worldCoordinate, buttons.Properties.IsRightButtonPressed);
    }

    public void Move(WorldEditor editor, PointerPoint buttons, Point worldCoordinate)
    {
        if (!_isPainting)
        {
            return;
        }

        Paint(worldCoordinate, buttons.Properties.IsRightButtonPressed);
    }

    public void Release(WorldEditor editor, PointerPoint buttons, Point worldCoordinate)
    {
        if (!_isPainting)
        {
            return;
        }

        _isPainting = false;
        editor.EndOperationAsync().Wait();
    }

    public void LeaveWindow(WorldEditor editor, PointerPoint buttons, Point worldCoordinate)
    {
        if (!_isPainting)
        {
            return;
        }

        _isPainting = false;
        editor.EndOperationAsync().Wait();
    }

    private void Paint(Point worldCoordinate, bool erase)
    {
        if (_editor == null)
        {
            return;
        }

        var center = new Vector2Int32((int)worldCoordinate.X, (int)worldCoordinate.Y);

        foreach (var point in _editor.GetShapePoints(center))
        {
            _editor.PaintPixel(point.X, point.Y, erase: erase);
        }
    }
}
