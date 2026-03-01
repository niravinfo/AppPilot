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
}
