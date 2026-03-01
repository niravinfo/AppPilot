using AppPilot.ViewModels;
using System.Windows;

namespace AppPilot.Views;

public partial class GitRepositoryEditorDialog : Window
{
    public GitRepositoryEditorDialog(GitRepositoryEditorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnSave(object sender, RoutedEventArgs e) => DialogResult = true;
    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
