using Avalonia;
using Avalonia.Input;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Media;
using TEdit5.Controls;
using TEdit.Editor;

namespace TEdit5.Editor.Tools;

/// <summary>吸取工具: 点击世界里的方块, 把它的编号与墙体取到当前画笔.</summary>
public partial class PickerTool : ReactiveObject, IMouseTool
{
    private readonly TilePicker _tilePicker;

    public PickerTool(TilePicker tilePicker)
    {
        _tilePicker = tilePicker;
    }

    public ICustomDrawOperation? DrawTool { get; }

    public string Name { get; } = "Picker";

    public string Tooltip { get; } = "吸取方块";

    [Reactive] private bool _isActive;

    public string IconName { get; } = "mdi-eyedropper";

    public Geometry Icon { get; } = AppIcons.Get("IconEyedropper20");

    public void Press(WorldEditor editor, PointerPoint buttons, Point worldCoordinate)
    {
        if (buttons.Properties.IsRightButtonPressed)
        {
            return;
        }

        var tile = editor.GetTileAt((int)worldCoordinate.X, (int)worldCoordinate.Y);

        if (tile.IsActive && tile.Type > 0)
        {
            _tilePicker.Tile = tile.Type;
            _tilePicker.TileStyleActive = true;
            _tilePicker.PaintMode = PaintMode.TileAndWall;
        }

        if (tile.Wall > 0)
        {
            _tilePicker.Wall = tile.Wall;
        }
    }
}
