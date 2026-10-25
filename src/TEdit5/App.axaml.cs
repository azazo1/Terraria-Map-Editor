using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using System;
using TEdit.Editor;
using TEdit5.Editor;
using TEdit5.Editor.Tools;
using TEdit5.Services;
using TEdit5.ViewModels;
using TEdit5.Views;

namespace TEdit5;

public partial class App : Application
{
    private IServiceProvider? _services;

    /// <summary>全局服务容器, 窗口在初始化时按需取用.</summary>
    public IServiceProvider Services =>
        _services ?? throw new InvalidOperationException("服务容器尚未初始化.");

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        LogManager.Initialize();
        LogManager.Info($"应用启动, 版本 {typeof(App).Assembly.GetName().Version}");

        var services = new ServiceCollection();

        // 视图模型
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<FileManagerViewModel>();

        // 编辑工具
        services.AddSingleton<ToolSelectionViewModel>();
        services.AddSingleton<PaletteViewModel>();
        services.AddSingleton<TilePicker>();

        // 文档与对话框
        services.AddSingleton<IDocumentService, DocumentService>();
        services.AddSingleton<IDialogService, DialogService>();

        services.AddSingleton<IMouseTool, ArrowTool>();
        services.AddSingleton<IMouseTool, BrushTool>();
        services.AddSingleton<IMouseTool, PencilTool>();
        services.AddSingleton<IMouseTool, PickerTool>();
        services.AddSingleton<IMouseTool, SelectTool>();

        _services = services.BuildServiceProvider();
        this.Resources[typeof(IServiceProvider)] = _services;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var viewModel = Services.GetRequiredService<MainWindowViewModel>();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel,
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainWindow
            {
                DataContext = viewModel,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
