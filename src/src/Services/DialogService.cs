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

    public bool Confirm(string message, string title = "Confirm") =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
}
