using AppPilot.Models;
using AppPilot.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace AppPilot.Views;

public partial class ServicesTab : UserControl
{
    public ServicesTab()
    {
        InitializeComponent();
    }

    private void MenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.ContextMenu != null)
        {
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.IsOpen = true;
        }
    }

    private void NpmCommandButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is NpmCommandConfig command)
        {
            // Find the parent ServiceItemViewModel
            if (button.DataContext is ServiceItemViewModel serviceVm)
            {
                serviceVm.RunNpmCommand(command.Name, command.Command);
            }
        }
    }
}
