using Avalonia.Controls;
using Avalonia.Interactivity;
using TEdit5.ViewModels;

namespace TEdit5.Views;

public partial class MaskSidebarView : UserControl
{
    public MaskSidebarView()
    {
        InitializeComponent();
    }

    private void OnClearAllClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is DocumentViewModel document)
        {
            document.ClearAllMasks();
        }
    }
}
