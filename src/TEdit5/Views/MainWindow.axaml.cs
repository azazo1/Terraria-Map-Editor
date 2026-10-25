using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Microsoft.Extensions.DependencyInjection;
using TEdit5.Services;
using TEdit5.ViewModels;

namespace TEdit5.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (Application.Current is App app)
        {
            // 文件对话框需要一个顶层窗口引用.
            app.Services.GetRequiredService<IDialogService>().AttachTo(this);
        }

        // 原生菜单只构建一次: Avalonia 的 macOS 菜单导出器不接受重复设置.
        if (!_menuBuilt)
        {
            _menuBuilt = true;
            BuildNativeMenu();
        }
    }

    /// <summary>
    /// macOS 上菜单应当挂在系统菜单栏, 而不是窗口里. 这里按原版的分组构建原生菜单.
    /// </summary>
    private bool _menuBuilt;

    private void BuildNativeMenu()
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        var file = Item("文件",
            Item("新建世界", vm.NewWorldCommand),
            new NativeMenuItemSeparator(),
            Item("打开…", vm.OpenWorldCommand, KeyGesture.Parse("Cmd+O")),
            Item("打开玩家存档…", vm.OpenPlayerCommand, KeyGesture.Parse("Cmd+Shift+O")),
            Item("保存玩家存档", vm.SavePlayerCommand, KeyGesture.Parse("Cmd+Alt+S")),
            Item("保存", vm.SaveCommand, KeyGesture.Parse("Cmd+S")),
            Item("另存为…", vm.SaveAsCommand, KeyGesture.Parse("Cmd+Shift+S")),
            new NativeMenuItemSeparator(),
            Item("关闭世界", vm.CloseDocumentCommand, KeyGesture.Parse("Cmd+W")));

        var edit = Item("编辑",
            Item("撤销", vm.UndoCommand, KeyGesture.Parse("Cmd+Z")),
            Item("重做", vm.RedoCommand, KeyGesture.Parse("Cmd+Shift+Z")));

        var view = Item("视图",
            Item("放大", vm.ZoomInCommand, KeyGesture.Parse("Cmd+OemPlus")),
            Item("缩小", vm.ZoomOutCommand, KeyGesture.Parse("Cmd+OemMinus")),
            Item("适应窗口", vm.FitToWindowCommand, KeyGesture.Parse("Cmd+D0")),
            Item("居中到世界中心", vm.CenterWorldCommand, KeyGesture.Parse("Cmd+Shift+C")),
            Item("显示/隐藏背景网格", vm.ToggleGridCommand, KeyGesture.Parse("Cmd+G")));

        var help = Item("帮助",
            Item("打开图标纹理目录", vm.OpenTextureFolderCommand),
            Item("关于 TEdit", vm.AboutCommand));

        var menu = new NativeMenu
        {
            file,
            edit,
            view,
            help,
        };

        NativeMenu.SetMenu(this, menu);
    }

    private static NativeMenuItem Item(string header, params NativeMenuItemBase[] children)
    {
        var item = new NativeMenuItem(header);
        if (children.Length > 0)
        {
            var submenu = new NativeMenu();
            foreach (var child in children)
            {
                submenu.Add(child);
            }

            item.Menu = submenu;
        }

        return item;
    }

    private static NativeMenuItem Item(string header, System.Windows.Input.ICommand command, KeyGesture? gesture = null)
        => new(header)
        {
            Command = command,
            Gesture = gesture,
        };
}
