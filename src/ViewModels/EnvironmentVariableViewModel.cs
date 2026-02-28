using CommunityToolkit.Mvvm.ComponentModel;

namespace AppPilot.ViewModels;

public partial class EnvironmentVariableViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _key = string.Empty;

    [ObservableProperty]
    private string _value = string.Empty;

    public EnvironmentVariableViewModel() { }

    public EnvironmentVariableViewModel(string key, string value)
    {
        _key = key;
        _value = value;
    }
}
