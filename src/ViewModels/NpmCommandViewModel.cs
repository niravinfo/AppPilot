using CommunityToolkit.Mvvm.ComponentModel;

namespace AppPilot.ViewModels;

/// <summary>
/// ViewModel for editing an npm command in the service editor.
/// </summary>
public partial class NpmCommandViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _command = string.Empty;

    /// <summary>
    /// Gets the first letter of the command name for display on buttons.
    /// </summary>
    public string Initial => string.IsNullOrEmpty(Name) ? "?" : Name[0].ToString().ToUpperInvariant();

    public NpmCommandViewModel() { }

    public NpmCommandViewModel(string name, string command)
    {
        _name = name;
        _command = command;
    }

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(Initial));
    }
}
