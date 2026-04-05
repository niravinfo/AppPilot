using AppPilot.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AppPilot.Views;

public partial class ServiceDiscoveryDialog : Window
{
    public ServiceDiscoveryDialog(ServiceDiscoveryViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnImport(object sender, RoutedEventArgs e) => DialogResult = true;
    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    private void ServiceRow_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is DiscoveredServiceItemViewModel vm)
        {
            if (DataContext is ServiceDiscoveryViewModel mainVm)
            {
                mainVm.SelectedService = vm;
            }
        }
    }

    private void BulkNewGroup_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is ServiceDiscoveryViewModel vm)
        {
            vm.AddNewGroupForBulkCommand.Execute(null);
            e.Handled = true;
        }
    }
}
