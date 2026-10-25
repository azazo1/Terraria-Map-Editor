namespace TEdit5.Controls;

/// <summary>
/// 界面图标. 取值与 WPF 版使用的 Fluent System Icons 码位一致, 便于和原版一一对应.
/// 这里只收录当前界面实际用到的符号, 后续移植页面时按需补充.
/// </summary>
public enum Symbol
{
    Empty = 0x0,

    DocumentSearch20 = 0xF3AE,
    DocumentAdd20 = 0xE4DB,
    Save20 = 0xF67F,
    SaveEdit20 = 0xEA48,
    SaveMultiple20 = 0xEA4B,
    FolderOpen20 = 0xF42E,
    QuestionCircle20 = 0xF63D,
    ArrowSync20 = 0xF190,
    Settings20 = 0xF6A9,
    Copy20 = 0xF32B,
    Cut20 = 0xF33D,
    ClipboardPaste20 = 0xE39F,
    ArrowUndo20 = 0xF1C0,
    ArrowRedo20 = 0xF1C1,
    Delete20 = 0xE74D,
    ZoomIn20 = 0xF7B7,
    ZoomOut20 = 0xF7B8,
    Crop20 = 0xE123,
    ArrowExpand20 = 0xF1A8,
    Globe20 = 0xE774,
    PaintBrush20 = 0xF1F6,
    Couch20 = 0xE4C0,
    Clipboard20 = 0xE77F,
    People20 = 0xE716,
    ChartMultiple20 = 0xE9D0,
    Trophy20 = 0xE7EE,
    Sparkle20 = 0xF5E3,
    Filter20 = 0xE71C,
    Search20 = 0xF78B,
    Code20 = 0xE943,
    Database20 = 0xE9CE,

    DocumentAdd24 = 0xE4DB,
    FolderOpen24 = 0xF42E,
    Save24 = 0xF67F,
    ZoomIn24 = 0xF7B7,
    ZoomOut24 = 0xF7B8,
    ArrowUndo24 = 0xF1C0,
    ArrowRedo24 = 0xF1C1,
    Delete24 = 0xE74D,
    Crop24 = 0xE123,
    ArrowExpand24 = 0xF1A8,
    Copy24 = 0xF32B,

    Cursor24 = 0xE444,
    Edit24 = 0xF3DE,
    Broom24 = 0xF202,
    Eyedropper20 = 0xE5FD,
    SquareMultiple24 = 0xF78C,
    PaintBrush24 = 0xF593,
}

public static class SymbolExtensions
{
    /// <summary>取图标的字符形式, 供 TextBlock 直接显示.</summary>
    public static string GetGlyph(this Symbol symbol) => char.ConvertFromUtf32((int)symbol);
}
