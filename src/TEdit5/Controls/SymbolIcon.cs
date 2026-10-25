using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TEdit5.Controls;

/// <summary>
/// 图标控件. 与 WPF 版的 SymbolIcon 对应: 用原版自带的 Segoe Fluent Icons 字体渲染字形,
/// 这样图标外观与原版一致, 而不是用字符或者 emoji 近似.
/// </summary>
public class SymbolIcon : TextBlock
{
    private static readonly FontFamily IconFont =
        new("avares://TEdit5/Assets/Fonts#Segoe Fluent Icons");

    public static readonly StyledProperty<Symbol> SymbolProperty =
        AvaloniaProperty.Register<SymbolIcon, Symbol>(nameof(Symbol), Symbol.Empty);

    static SymbolIcon()
    {
        SymbolProperty.Changed.AddClassHandler<SymbolIcon>((icon, _) => icon.UpdateGlyph());
    }

    public SymbolIcon()
    {
        FontFamily = IconFont;
        TextAlignment = TextAlignment.Center;
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
    }

    public Symbol Symbol
    {
        get => GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    private void UpdateGlyph() => Text = Symbol.GetGlyph();
}
