using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TEdit.Editor;
using TEdit.Terraria;
using TEdit5.ViewModels;

namespace TEdit5.Services;

/// <summary>已打开的文档集合, 负责世界的装载与关闭.</summary>
public interface IDocumentService
{
    ObservableCollection<DocumentViewModel> Documents { get; }

    /// <summary>从磁盘装载世界并加入文档列表. 失败时返回 null.</summary>
    Task<DocumentViewModel?> LoadWorldAsync(string fileName, IProgress<ProgressChangedEventArgs>? progress = null);

    /// <summary>把文档保存到指定路径.</summary>
    Task SaveWorldAsync(DocumentViewModel document, string fileName, IProgress<ProgressChangedEventArgs>? progress = null);

    /// <summary>关闭文档并释放撤销缓存.</summary>
    void CloseDocument(DocumentViewModel document);
}

public sealed class DocumentService : ReactiveObject, IDocumentService
{
    private readonly IServiceProvider _services;

    public ObservableCollection<DocumentViewModel> Documents { get; } = new();

    public DocumentService(IServiceProvider services)
    {
        _services = services;
    }

    public async Task<DocumentViewModel?> LoadWorldAsync(
        string fileName,
        IProgress<ProgressChangedEventArgs>? progress = null)
    {
        LogManager.Info($"装载世界: {fileName}");

        var (world, error) = await World.LoadWorldAsync(fileName, progress: progress);

        if (world == null)
        {
            LogManager.Exception(error ?? new InvalidOperationException("世界装载失败, 未返回错误详情."), $"装载世界失败: {fileName}");
            return null;
        }

        var toolSelection = _services.GetRequiredService<ToolSelectionViewModel>();
        var tilePicker = _services.GetRequiredService<TilePicker>();

        var document = new DocumentViewModel(world, toolSelection, tilePicker, fileName);
        Documents.Add(document);

        LogManager.Info($"世界装载完成: {world.Title}, {world.TilesWide}x{world.TilesHigh} 方块");
        return document;
    }

    public Task SaveWorldAsync(
        DocumentViewModel document,
        string fileName,
        IProgress<ProgressChangedEventArgs>? progress = null) =>
        document.SaveAsAsync(fileName, progress);

    public void CloseDocument(DocumentViewModel document)
    {
        if (!Documents.Remove(document))
        {
            return;
        }

        LogManager.Info($"关闭文档: {document.Title}");
        document.Dispose();
    }
}
