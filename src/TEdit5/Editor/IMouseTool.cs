using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using TEdit5.Controls;
using TEdit.Editor;

namespace TEdit5.Editor;

/// <summary>
/// 世界视图上的鼠标工具. 与 Windows 版的 IEditorTool 对应, 但直接接收世界坐标,
/// 由渲染控件把屏幕坐标换算好再传进来.
/// </summary>
public interface IMouseTool
{
    ICustomDrawOperation? DrawTool { get; }

    string Name { get; }

    string Tooltip { get; }

    bool IsActive { get; set; }

    /// <summary>工具条上显示的图标名, 保留给以后接入图标字体.</summary>
    string IconName { get; }

    /// <summary>工具条图标, 使用矢量路径绘制.</summary>
    Geometry Icon { get; }

    void Move(WorldEditor editor, PointerPoint buttons, Point worldCoordinate) { }

    void Press(WorldEditor editor, PointerPoint buttons, Point worldCoordinate) { }

    void Release(WorldEditor editor, PointerPoint buttons, Point worldCoordinate) { }

    void LeaveWindow(WorldEditor editor, PointerPoint buttons, Point worldCoordinate) { }

    void Scroll(int delta, Point worldCoordinate) { }
}
