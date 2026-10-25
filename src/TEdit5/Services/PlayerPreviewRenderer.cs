using System;
using Avalonia.Media.Imaging;
using SkiaSharp;
using TEdit.Common;
using TEdit.Terraria;
using TEdit.Terraria.Player;

namespace TEdit5.Services;

/// <summary>
/// 用角色身上的七种配色画一张示意预览图. 不依赖游戏素材,
/// 只表达配色关系, 方便在编辑器里直观确认改色结果.
/// </summary>
public static class PlayerPreviewRenderer
{
    private const int Width = 48;
    private const int Height = 64;

    /// <summary>按角色配色画出 PNG 字节, 不依赖 Avalonia 平台, 可离线测试.</summary>
    public static byte[]? RenderPng(PlayerCharacter? player)
    {
        if (player == null)
        {
            return null;
        }

        var appearance = player.Appearance;
        using var bitmap = new SKBitmap(new SKImageInfo(Width, Height, SKColorType.Bgra8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint { IsAntialias = true };

        canvas.Clear(SKColors.Transparent);

        // 头发盖住头顶, 脸部留出肤色, 身体与四肢按配色分块.
        Draw(canvas, paint, appearance.HairColor, 12, 4, 24, 12);
        Draw(canvas, paint, appearance.SkinColor, 14, 12, 20, 12);
        Draw(canvas, paint, appearance.EyeColor, 18, 16, 4, 4);
        Draw(canvas, paint, appearance.EyeColor, 26, 16, 4, 4);
        Draw(canvas, paint, appearance.ShirtColor, 12, 24, 24, 18);
        Draw(canvas, paint, appearance.UnderShirtColor, 12, 24, 24, 4);
        Draw(canvas, paint, appearance.PantsColor, 14, 42, 20, 14);
        Draw(canvas, paint, appearance.ShoeColor, 14, 56, 20, 6);
        Draw(canvas, paint, appearance.SkinColor, 4, 24, 8, 16);
        Draw(canvas, paint, appearance.SkinColor, 36, 24, 8, 16);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>生成界面用的预览图, 需要 Avalonia 平台已初始化.</summary>
    public static Bitmap? Render(PlayerCharacter? player)
    {
        byte[]? png = RenderPng(player);
        if (png == null)
        {
            return null;
        }

        using var stream = new System.IO.MemoryStream(png);
        return new Bitmap(stream);
    }

    private static void Draw(SKCanvas canvas, SKPaint paint, TEditColor color, int x, int y, int width, int height)
    {
        paint.Color = new SKColor(color.R, color.G, color.B, 255);
        canvas.DrawRect(x, y, width, height, paint);
    }
}
