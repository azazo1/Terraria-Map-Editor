using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TEdit5.Controls;

/// <summary>
/// 五角星评分控件. 按 <see cref="Value"/> 点亮前若干颗星,
/// 取代原先用实心与空心字符拼出来的星级文本.
/// </summary>
public class StarRating : Control
{
    private const double InnerRatio = 0.382;

    public static readonly StyledProperty<int> ValueProperty =
        AvaloniaProperty.Register<StarRating, int>(nameof(Value));

    public static readonly StyledProperty<int> MaxValueProperty =
        AvaloniaProperty.Register<StarRating, int>(nameof(MaxValue), 5);

    public static readonly StyledProperty<double> StarSizeProperty =
        AvaloniaProperty.Register<StarRating, double>(nameof(StarSize), 12d);

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<StarRating, IBrush?>(nameof(Fill), Brushes.Gold);

    public static readonly StyledProperty<IBrush?> EmptyFillProperty =
        AvaloniaProperty.Register<StarRating, IBrush?>(nameof(EmptyFill), Brushes.DimGray);

    static StarRating()
    {
        AffectsRender<StarRating>(ValueProperty, MaxValueProperty, StarSizeProperty, FillProperty, EmptyFillProperty);
    }

    /// <summary>点亮的星数.</summary>
    public int Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>星星总数, 默认五颗.</summary>
    public int MaxValue
    {
        get => GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
    }

    /// <summary>单颗星的外接半径.</summary>
    public double StarSize
    {
        get => GetValue(StarSizeProperty);
        set => SetValue(StarSizeProperty, value);
    }

    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public IBrush? EmptyFill
    {
        get => GetValue(EmptyFillProperty);
        set => SetValue(EmptyFillProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        int total = Math.Max(0, MaxValue);
        if (total == 0 || StarSize <= 0)
        {
            return;
        }

        int lit = Clamp(Value, total);
        double step = StarSize * 2.2;

        for (int index = 0; index < total; index++)
        {
            var center = new Point(StarSize + index * step, StarSize);
            var geometry = BuildStarGeometry(center, StarSize);
            var brush = index < lit ? Fill : EmptyFill;
            context.DrawGeometry(brush, null, geometry);
        }
    }

    /// <summary>把星数夹到 0 与总数之间.</summary>
    public static int Clamp(int value, int maxValue) => Math.Clamp(value, 0, Math.Max(0, maxValue));

    /// <summary>按外接半径生成五角星轮廓.</summary>
    public static StreamGeometry BuildStarGeometry(Point center, double radius)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var points = BuildStarPoints(center, radius);
            ctx.BeginFigure(points[0], true);
            for (int index = 1; index < points.Length; index++)
            {
                ctx.LineTo(points[index]);
            }

            ctx.EndFigure(true);
        }

        return geometry;
    }

    /// <summary>五角星的十个顶点, 外顶点与内顶点交替.</summary>
    public static Point[] BuildStarPoints(Point center, double radius)
    {
        const int tips = 5;
        var points = new Point[tips * 2];
        double inner = radius * InnerRatio;

        for (int index = 0; index < points.Length; index++)
        {
            double current = index % 2 == 0 ? radius : inner;
            double angle = -Math.PI / 2 + index * Math.PI / tips;
            points[index] = new Point(
                center.X + Math.Cos(angle) * current,
                center.Y + Math.Sin(angle) * current);
        }

        return points;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        int total = Math.Max(1, MaxValue);
        return new Size(total * StarSize * 2.2, StarSize * 2);
    }
}
