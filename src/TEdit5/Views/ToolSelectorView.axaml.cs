using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using TEdit5.Editor;
using TEdit5.ViewModels;

namespace TEdit5.Views;

public partial class ToolSelectorView : UserControl
{
    public ToolSelectorView()
    {
        InitializeComponent();
    }

    private void OnToolClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton button || button.DataContext is not IMouseTool tool)
        {
            return;
        }

        if (DataContext is ToolSelectionViewModel selection)
        {
            selection.Select(tool);
        }
    }
}
