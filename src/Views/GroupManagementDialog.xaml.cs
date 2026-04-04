using AppPilot.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AppPilot.Views;

public partial class GroupManagementDialog : Window
{
    private GroupItemViewModel? _activeColorItem;

    public GroupManagementDialog(GroupManagementViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnSave(object sender, RoutedEventArgs e) => DialogResult = true;
    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    private void NewGroupTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter && DataContext is GroupManagementViewModel vm)
        {
            vm.AddGroupCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void ColorSwatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not GroupItemViewModel item) return;

        _activeColorItem = item;
        ColorPickerPopup.PlacementTarget = btn;
        ColorPickerPopup.IsOpen = true;
    }

    private void PaletteColor_Click(object sender, RoutedEventArgs e)
    {
        if (_activeColorItem == null || sender is not Button btn) return;

        if (btn.Background is SolidColorBrush brush)
        {
            _activeColorItem.ColorCode = $"#{brush.Color.R:X2}{brush.Color.G:X2}{brush.Color.B:X2}";
        }

        ColorPickerPopup.IsOpen = false;
    }

    private void ClearColor_Click(object sender, RoutedEventArgs e)
    {
        if (_activeColorItem == null) return;

        _activeColorItem.ColorCode = string.Empty;
        ColorPickerPopup.IsOpen = false;
    }
}
