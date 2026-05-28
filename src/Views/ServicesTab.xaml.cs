using AppPilot.Models;
using AppPilot.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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
            // The button's DataContext is NpmCommandConfig (the ItemsControl item).
            // Walk up the visual tree to find the ServiceItemViewModel on the parent.
            DependencyObject? current = button;
            while (current != null)
            {
                current = VisualTreeHelper.GetParent(current);
                if (current is FrameworkElement { DataContext: ServiceItemViewModel serviceVm })
                {
                    serviceVm.RunNpmCommand(command.Name, command.Command);
                    return;
                }
            }
        }
    }
}
