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
}
