using AppPilot.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace AppPilot.Views;

public partial class ServiceEditorDialog : Window
{
    public ServiceEditorDialog(ServiceEditorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnSave(object sender, RoutedEventArgs e) => DialogResult = true;
    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    private void NewGroupTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        if (DataContext is ServiceEditorViewModel vm)
        {
            vm.AddNewGroupCommand.Execute(null);
        }

        e.Handled = true;
    }
}
