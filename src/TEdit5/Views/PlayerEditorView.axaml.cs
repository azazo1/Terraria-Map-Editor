using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using TEdit5.ViewModels;

namespace TEdit5.Views;

public partial class PlayerEditorView : UserControl
{
    public PlayerEditorView()
    {
        InitializeComponent();
    }

    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top == null)
        {
            return;
        }

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "打开玩家存档",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("玩家存档")
                {
                    Patterns = new[] { "*.plr" },
                },
            },
        });

        if (files.Count == 0)
        {
            return;
        }

        string? path = files[0].TryGetLocalPath();
        if (path == null)
        {
            return;
        }

        (DataContext as DocumentViewModel)?.Player.Load(path);
    }

    private void OnReloadClick(object? sender, RoutedEventArgs e)
    {
        (DataContext as DocumentViewModel)?.Player.Reload();
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        var vm = DataContext as DocumentViewModel;
        if (top == null || vm == null)
        {
            return;
        }

        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "保存玩家存档",
            SuggestedFileName = "player.plr",
            DefaultExtension = "plr",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("玩家存档")
                {
                    Patterns = new[] { "*.plr" },
                },
            },
        });

        string? path = file?.TryGetLocalPath();
        if (path == null)
        {
            return;
        }

        vm.Player.Save(path);
    }
}
