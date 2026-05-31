using AppPilot.ViewModels;
using System.Text.RegularExpressions;
using System.Windows;
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

        // Re-center after resize (prefer owner, fall back to work area)
        if (Owner != null)
        {
            Left = Owner.Left + (Owner.ActualWidth - Width) / 2;
            Top = Owner.Top + (Owner.ActualHeight - Height) / 2;
        }
        else
        {
            Left = (workArea.Width - Width) / 2 + workArea.Left;
            Top = (workArea.Height - Height) / 2 + workArea.Top;
        }
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

    private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        // Allow only digits
        e.Handled = !IsNumeric(e.Text);
    }

    private void NumericTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(typeof(string)))
        {
            var text = (string)e.DataObject.GetData(typeof(string));
            if (!IsNumeric(text))
            {
                e.CancelCommand();
            }
        }
        else
        {
            e.CancelCommand();
        }
    }

    private static bool IsNumeric(string text)
    {
        return Regex.IsMatch(text, "^[0-9]+$");
    }
}
