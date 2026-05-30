using AppPilot.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AppPilot.ViewModels;

/// <summary>
/// ViewModel for displaying a profile in dropdowns and lists.
/// </summary>
public partial class ProfileItemViewModel : ViewModelBase
{
    public ProfileConfig Config { get; }

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private bool _isDefault;

    [ObservableProperty]
    private int _serviceCount;

    public string Id => Config.Id;

    public ProfileItemViewModel(ProfileConfig config)
    {
        Config = config;
        Name = config.Name;
        Description = config.Description;
        IsDefault = config.IsDefault;
        ServiceCount = config.ServiceNames.Count;
    }

    public void UpdateFromConfig()
    {
        Name = Config.Name;
        Description = Config.Description;
        IsDefault = Config.IsDefault;
        ServiceCount = Config.ServiceNames.Count;
    }

    public override string ToString() => Name;
}
