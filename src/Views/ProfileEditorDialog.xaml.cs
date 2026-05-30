using AppPilot.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AppPilot.Views;

public partial class ProfileEditorDialog : Window
{
    public ProfileEditorDialog(ProfileEditorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Ensure window fits on small screens
        var workArea = SystemParameters.WorkArea;
        if (Height > workArea.Height * 0.9)
        {
            Height = workArea.Height * 0.9;
        }
        if (Width > workArea.Width * 0.9)
        {
            Width = workArea.Width * 0.9;
        }

        // Center on screen after resize
        Left = (workArea.Width - Width) / 2 + workArea.Left;
        Top = (workArea.Height - Height) / 2 + workArea.Top;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (DataContext is ProfileEditorViewModel vm && !vm.CanSave)
        {
            MessageBox.Show("Please enter a profile name.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    private void AvailableServices_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ProfileEditorViewModel vm && vm.SelectedAvailableService != null)
        {
            vm.AddServiceCommand.Execute(null);
        }
    }

    private void ProfileServices_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ProfileEditorViewModel vm && vm.SelectedProfileService != null)
        {
            vm.RemoveServiceCommand.Execute(null);
        }
    }
}
