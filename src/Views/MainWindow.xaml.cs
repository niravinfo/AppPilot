using AppPilot.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace AppPilot.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += (s, e) => viewModel.Initialize();
        Closing += (s, e) => viewModel.Shutdown();
        viewModel.FocusSearchRequested += () =>
        {
            SearchTextBox.Focus();
        };
    }

    private void DefaultProfile_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.ClearSelectedProfileCommand.Execute(null);
            ProfileComboBox.IsDropDownOpen = false;
        }
    }
}
