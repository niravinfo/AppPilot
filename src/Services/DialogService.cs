using AppPilot.ViewModels;
using AppPilot.Views;
using System.Windows;

namespace AppPilot.Services;

public class DialogService : IDialogService
{
    public bool? ShowServiceEditor(ServiceEditorViewModel vm)
    {
        var dialog = new ServiceEditorDialog(vm)
        {
            Owner = Application.Current.MainWindow
        };
        return dialog.ShowDialog();
    }

    public bool? ShowGitRepositoryEditor(GitRepositoryEditorViewModel vm)
    {
        var dialog = new GitRepositoryEditorDialog(vm)
        {
            Owner = Application.Current.MainWindow
        };
        return dialog.ShowDialog();
    }

    public bool? ShowServiceDiscovery(ServiceDiscoveryViewModel vm)
    {
        var dialog = new ServiceDiscoveryDialog(vm)
        {
            Owner = Application.Current.MainWindow
        };
        return dialog.ShowDialog();
    }

    public bool? ShowGroupManagement(GroupManagementViewModel vm)
    {
        var dialog = new GroupManagementDialog(vm)
        {
            Owner = Application.Current.MainWindow
        };
        return dialog.ShowDialog();
    }

    public bool Confirm(string message, string title = "Confirm") =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
}
