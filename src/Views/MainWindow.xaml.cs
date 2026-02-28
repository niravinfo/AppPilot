using AppPilot.ViewModels;
using System.Windows;

namespace AppPilot.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += (s, e) => viewModel.Initialize();
        Closing += (s, e) => viewModel.Shutdown();
    }
}
