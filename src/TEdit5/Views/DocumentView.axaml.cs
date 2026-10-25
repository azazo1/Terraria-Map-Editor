using System;
using Avalonia;
using Avalonia.Controls;
using TEdit.Geometry;
using TEdit5.Controls;
using TEdit5.ViewModels;

namespace TEdit5.Views;

public partial class DocumentView : UserControl
{
    private DocumentViewModel? _document;

    public DocumentView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += OnDetached;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        DetachDocument();

        _document = DataContext as DocumentViewModel;
        if (_document == null)
        {
            return;
        }

        _document.TilesChanged += OnTilesChanged;
        _document.RenderInvalidated += OnRenderInvalidated;
        _document.FocusRequested += OnFocusRequested;
        _document.FitRequested += OnFitRequested;

        // 过滤器接入渲染: 条件变化时重建图块.
        TEdit5.Controls.WorldRenderEngine.Layers.RasterTileRenderer.Filter = _document.Filters.Filter;
        _document.Filters.Filter.PropertyChanged += OnFilterChanged;
    }

    private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e) => DetachDocument();

    private void DetachDocument()
    {
        if (_document == null)
        {
            return;
        }

        _document.TilesChanged -= OnTilesChanged;
        _document.RenderInvalidated -= OnRenderInvalidated;
        _document.FocusRequested -= OnFocusRequested;
        _document.FitRequested -= OnFitRequested;
        _document.Filters.Filter.PropertyChanged -= OnFilterChanged;
        TEdit5.Controls.WorldRenderEngine.Layers.RasterTileRenderer.Filter = null;
        _document = null;
    }

    private void OnTilesChanged(object? sender, TileChangedEventArgs e) =>
        RenderBox?.NotifyTilesChanged(e.X, e.Y, e.Width, e.Height);

    private void OnRenderInvalidated(object? sender, EventArgs e) =>
        RenderBox?.NotifyWorldChanged();

    /// <summary>把视口滚动到目标方块, 用于 NPC 列表的定位.</summary>
    /// <summary>过滤条件变化后重建整幅图块.</summary>
    private void OnFilterChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TEdit5.Services.WorldFilter.Revision))
        {
            RenderBox?.NotifyWorldChanged();
        }
    }

    private void OnFitRequested(object? sender, EventArgs e) => RenderBox?.ZoomToFit();

    private void OnFocusRequested(object? sender, Vector2Int32 tile)
    {
        var box = RenderBox;
        if (box == null)
        {
            return;
        }

        double scale = box.Zoom / 100d;
        double targetX = tile.X * scale;
        double targetY = tile.Y * scale;

        box.Offset = new Vector(
            targetX - box.Bounds.Width / 2,
            targetY - box.Bounds.Height / 2);
    }

    private SkiaWorldRenderBox? RenderBox => this.FindControl<SkiaWorldRenderBox>("WorldRenderBox");
}
