using Avalonia;
using Avalonia.Input;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Media;
using TEdit5.Controls;
using TEdit.Editor;
using TEdit.Geometry;
using TEdit.Terraria;

namespace TEdit5.Editor.Tools;

/// <summary>
/// 铅笔工具: 单格绘制. 左键连续画线, 右键擦除, 拖动时按直线补齐中间格子.
/// </summary>
public partial class PencilTool : ReactiveObject, IMouseTool
{
    public ICustomDrawOperation? DrawTool { get; }

    public string Name { get; } = "Pencil";

    public string Tooltip { get; } = "铅笔 (左键绘制, 右键擦除)";

    [Reactive] private bool _isActive;

    public string IconName { get; } = "mdi-pencil";

    /// <summary>原版工具栏使用的位图图标, 取自仓库 docs/Icons.</summary>
    public Avalonia.Media.Imaging.Bitmap? IconBitmap { get; } = AppIconAssets.Load("pencil.png");

    public bool HasBitmapIcon => IconBitmap != null;

    public Geometry Icon { get; } = AppIcons.Get("IconEdit24");

    private bool _isLeftDown;
    private bool _isRightDown;
    private Vector2Int32 _startPoint;
    private Vector2Int32 _endPoint;
    private WorldEditor? _editor;

    private void CheckDirectionAndDraw(Vector2Int32 tile)
    {
        Vector2Int32 p = tile;
        if (_isRightDown)
        {
            if (_isLeftDown)
            {
                p.X = _startPoint.X;
            }
            else
            {
                p.Y = _startPoint.Y;
            }

            DrawLine(p, erase: true);
            _startPoint = p;
        }
        else if (_isLeftDown)
        {
            DrawLine(p, erase: false);
            _startPoint = p;
            _endPoint = p;
        }
    }

    private void DrawLine(Vector2Int32 to, bool erase)
    {
        if (_editor == null)
        {
            return;
        }

        foreach (Vector2Int32 pixel in Shape.DrawLineTool(_startPoint, to))
        {
            _editor.PaintPixel(pixel.X, pixel.Y, erase: erase);
        }
    }

    public void Press(WorldEditor editor, PointerPoint buttons, Point worldCoordinate)
    {
        _editor = editor;
        _editor.BeginOperationAsync().Wait();

        if (!_isRightDown && !_isLeftDown)
        {
            _startPoint = new Vector2Int32((int)worldCoordinate.X, (int)worldCoordinate.Y);
        }

        _isLeftDown = buttons.Properties.IsLeftButtonPressed;
        _isRightDown = buttons.Properties.IsRightButtonPressed;

        CheckDirectionAndDraw(new Vector2Int32((int)worldCoordinate.X, (int)worldCoordinate.Y));
    }

    public void Move(WorldEditor editor, PointerPoint buttons, Point worldCoordinate)
    {
        _isLeftDown = buttons.Properties.IsLeftButtonPressed;
        _isRightDown = buttons.Properties.IsRightButtonPressed;

        CheckDirectionAndDraw(new Vector2Int32((int)worldCoordinate.X, (int)worldCoordinate.Y));
    }

    public void Release(WorldEditor editor, PointerPoint buttons, Point worldCoordinate)
    {
        CheckDirectionAndDraw(new Vector2Int32((int)worldCoordinate.X, (int)worldCoordinate.Y));

        _isLeftDown = buttons.Properties.IsLeftButtonPressed;
        _isRightDown = buttons.Properties.IsRightButtonPressed;

        if (!_isLeftDown && !_isRightDown)
        {
            editor.EndOperationAsync().Wait();
        }
    }
}
