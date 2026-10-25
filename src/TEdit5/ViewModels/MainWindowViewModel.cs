using System;
using System.Reactive.Linq;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI.Primitives;
using TEdit.Editor;
using TEdit5.Controls.WorldRenderEngine.Layers;
using TEdit5.Services;

namespace TEdit5.ViewModels;

/// <summary>主窗口视图模型: 菜单命令, 文档切换与状态栏进度.</summary>
public partial class MainWindowViewModel : ReactiveObject
{
    private readonly IDialogService _dialogService;
    private readonly TilePicker _tilePicker;

    public IDocumentService DocumentService { get; }

    /// <summary>窗口标题, 与原版一致显示当前世界名.</summary>
    public string WindowTitle => SelectedDocument == null
        ? "TEdit"
        : $"TEdit - {SelectedDocument.Title}{(SelectedDocument.IsDirty ? " *" : string.Empty)}";

    public PaletteViewModel Palette { get; }

    private DocumentViewModel? _previousDocument;

    /// <summary>当前是否有打开的文档, 决定保存与关闭类入口是否可用.</summary>
    [Reactive] private bool _hasDocument;

    /// <summary>当前文档是否还有可撤销的步骤.</summary>
    [Reactive] private bool _canUndo;

    /// <summary>当前文档是否还有可重做的步骤.</summary>
    [Reactive] private bool _canRedo;

    private async Task OpenPlayerAsync()
    {
        string? path = await _dialogService.PickPlayerToOpenAsync();
        if (path == null || SelectedDocument == null)
        {
            return;
        }

        SelectedDocument.Player.Load(path);
        SelectedDocument.SelectedPanelIndex = 14;
    }

    private async Task SavePlayerAsync()
    {
        if (SelectedDocument == null)
        {
            return;
        }

        var editor = SelectedDocument.Player;
        if (editor.SaveCurrent())
        {
            return;
        }

        string? path = await _dialogService.PickPlayerToSaveAsync("player.plr");
        if (path != null)
        {
            editor.Save(path);
        }
    }

    private static void OpenTextureFolder()
    {
        try
        {
            var settings = TEdit5.Services.AppSettings.Load();
            System.IO.Directory.CreateDirectory(settings.TexturePath);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "open",
                Arguments = settings.TexturePath,
                UseShellExecute = false,
            });
            TEdit5.Services.LogManager.Info($"已打开纹理目录: {settings.TexturePath}");
        }
        catch (System.Exception ex)
        {
            TEdit5.Services.LogManager.Error($"打开纹理目录失败: {ex.Message}");
        }
    }

    private void SyncHistoryState()
    {
        HasDocument = SelectedDocument != null;
        CanUndo = SelectedDocument?.CanUndo ?? false;
        CanRedo = SelectedDocument?.CanRedo ?? false;
    }

    /// <summary>世界标题变化或者脏标记变化时刷新窗口标题.</summary>
    private void OnDocumentPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DocumentViewModel.Title) or nameof(DocumentViewModel.IsDirty))
        {
            this.RaisePropertyChanged(nameof(WindowTitle));
        }

        if (e.PropertyName is nameof(DocumentViewModel.CanUndo) or nameof(DocumentViewModel.CanRedo))
        {
            SyncHistoryState();
        }
    }

    [Reactive] private DocumentViewModel? _selectedDocument;
    [Reactive] private ToolSelectionViewModel _toolSelection;
    [Reactive] private int _progressPercentage;
    [Reactive] private string _progressText = string.Empty;
    [Reactive] private RenderLayerVisibility _renderLayerVisibility = new();

    public ReactiveCommand<RxVoid, RxVoid> OpenWorldCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> SaveCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> SaveAsCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> CloseDocumentCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ExitCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> NewWorldCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> UndoCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> RedoCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ZoomInCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> ZoomOutCommand { get; }

    /// <summary>把视图移到世界中心.</summary>
    public ReactiveCommand<RxVoid, RxVoid> CenterWorldCommand { get; }

    /// <summary>打开图标纹理目录, 方便用户放入 Item_{id}.png.</summary>
    public ReactiveCommand<RxVoid, RxVoid> OpenTextureFolderCommand { get; }

    /// <summary>打开玩家存档并切到玩家页签.</summary>
    public ReactiveCommand<RxVoid, RxVoid> OpenPlayerCommand { get; }

    /// <summary>保存玩家存档, 没有打开路径时先选一个.</summary>
    public ReactiveCommand<RxVoid, RxVoid> SavePlayerCommand { get; }

    /// <summary>缩放到适应窗口.</summary>
    public ReactiveCommand<RxVoid, RxVoid> FitToWindowCommand { get; }

    /// <summary>切换背景网格显示.</summary>
    public ReactiveCommand<RxVoid, RxVoid> ToggleGridCommand { get; }

    public ReactiveCommand<RxVoid, RxVoid> AboutCommand { get; }

    public MainWindowViewModel(
        IDocumentService documentService,
        ToolSelectionViewModel toolSelection,
        IDialogService dialogService,
        PaletteViewModel palette,
        TilePicker tilePicker)
    {
        _tilePicker = tilePicker;
        DocumentService = documentService;
        _toolSelection = toolSelection;
        _dialogService = dialogService;
        Palette = palette;

        // 切换文档时刷新窗口标题.
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SelectedDocument))
            {
                if (_previousDocument != null)
                {
                    _previousDocument.PropertyChanged -= OnDocumentPropertyChanged;
                }

                _previousDocument = SelectedDocument;
                if (_previousDocument != null)
                {
                    _previousDocument.PropertyChanged += OnDocumentPropertyChanged;
                }

                SyncHistoryState();
                this.RaisePropertyChanged(nameof(WindowTitle));
            }
        };

        OpenWorldCommand = ReactiveCommand.CreateFromTask(OpenWorldAsync);
        SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync, this.WhenAnyValue(x => x.HasDocument));
        SaveAsCommand = ReactiveCommand.CreateFromTask(SaveAsAsync, this.WhenAnyValue(x => x.HasDocument));
        CloseDocumentCommand = ReactiveCommand.Create(CloseDocument, this.WhenAnyValue(x => x.HasDocument));
        ExitCommand = ReactiveCommand.Create(Exit);
        NewWorldCommand = ReactiveCommand.Create(NewWorld);
        // 撤销与重做跟随当前文档的历史状态, 无历史时菜单项自动置灰.
        UndoCommand = ReactiveCommand.Create(() => SelectedDocument?.Undo(), this.WhenAnyValue(x => x.CanUndo));
        RedoCommand = ReactiveCommand.Create(() => SelectedDocument?.Redo(), this.WhenAnyValue(x => x.CanRedo));
        OpenTextureFolderCommand = ReactiveCommand.Create(OpenTextureFolder);
        OpenPlayerCommand = ReactiveCommand.CreateFromTask(OpenPlayerAsync);
        SavePlayerCommand = ReactiveCommand.CreateFromTask(SavePlayerAsync);

        FitToWindowCommand = ReactiveCommand.Create(
            () => SelectedDocument?.RequestFit(),
            this.WhenAnyValue(x => x.HasDocument));

        CenterWorldCommand = ReactiveCommand.Create(
            () =>
            {
                var doc = SelectedDocument;
                if (doc?.World == null)
                {
                    return;
                }

                doc.RequestFocus(doc.World.TilesWide / 2, doc.World.TilesHigh / 2);
            },
            this.WhenAnyValue(x => x.HasDocument));

        ToggleGridCommand = ReactiveCommand.Create(
            () =>
            {
                TEdit5.Controls.WorldRenderEngine.Layers.BackgroundGridCustomDrawOp.Visible =
                    !TEdit5.Controls.WorldRenderEngine.Layers.BackgroundGridCustomDrawOp.Visible;
                SelectedDocument?.RequestRedraw();
            },
            this.WhenAnyValue(x => x.HasDocument));

        ZoomInCommand = ReactiveCommand.Create(() => ZoomBy(25));
        ZoomOutCommand = ReactiveCommand.Create(() => ZoomBy(-25));
        AboutCommand = ReactiveCommand.Create(About);
    }

    private async Task OpenWorldAsync()
    {
        var fileName = await _dialogService.PickWorldToOpenAsync();
        if (fileName == null)
        {
            return;
        }

        ProgressText = "正在装载世界...";
        var document = await DocumentService.LoadWorldAsync(fileName, CreateProgress());

        ProgressPercentage = 0;
        ProgressText = document == null ? "世界装载失败, 详情见日志" : string.Empty;

        if (document != null)
        {
            SelectedDocument = document;
        }
    }

    private async Task SaveAsync()
    {
        if (SelectedDocument == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(SelectedDocument.FileName))
        {
            await SaveAsAsync();
            return;
        }

        await DocumentService.SaveWorldAsync(SelectedDocument, SelectedDocument.FileName, CreateProgress());
        ProgressPercentage = 0;
        ProgressText = string.Empty;
    }

    private async Task SaveAsAsync()
    {
        if (SelectedDocument == null)
        {
            return;
        }

        string suggested = string.IsNullOrEmpty(SelectedDocument.FileName)
            ? $"{SelectedDocument.World?.Title ?? "world"}.wld"
            : System.IO.Path.GetFileName(SelectedDocument.FileName);

        var fileName = await _dialogService.PickWorldToSaveAsync(suggested);
        if (fileName == null)
        {
            return;
        }

        ProgressText = "正在保存世界...";
        await DocumentService.SaveWorldAsync(SelectedDocument, fileName, CreateProgress());

        ProgressPercentage = 0;
        ProgressText = string.Empty;
    }

    private void CloseDocument()
    {
        if (SelectedDocument == null)
        {
            return;
        }

        var closing = SelectedDocument;
        DocumentService.CloseDocument(closing);

        SelectedDocument = DocumentService.Documents.Count > 0
            ? DocumentService.Documents[0]
            : null;
    }

    private void NewWorld()
    {
        const int width = 800;
        const int height = 600;

        var world = new TEdit.Terraria.World(height, width, "新世界", seed: Environment.TickCount)
        {
            Version = TEdit.Terraria.WorldConfiguration.CompatibleVersion,
            Tiles = new TEdit.Terraria.Tile[width, height],
        };

        var document = new DocumentViewModel(world, ToolSelection, _tilePicker);
        DocumentService.Documents.Add(document);
        SelectedDocument = document;

        LogManager.Info("新建空白世界, 地形生成尚未接入");
    }

    private void ZoomBy(int delta)
    {
        if (SelectedDocument == null)
        {
            return;
        }

        int zoom = Math.Clamp(SelectedDocument.Zoom + delta, SelectedDocument.MinZoom, SelectedDocument.MaxZoom);
        SelectedDocument.Zoom = zoom;
    }

    private static void About()
    {
        LogManager.Info("TEdit5 (Avalonia) 开发中版本");
    }

    private static void Exit()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    private IProgress<ProgressChangedEventArgs> CreateProgress() =>
        new Progress<ProgressChangedEventArgs>(args =>
        {
            ProgressPercentage = args.ProgressPercentage;
            ProgressText = args.UserState as string ?? string.Empty;
        });
}
