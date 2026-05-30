using AppPilot.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System;

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
        ArgumentNullException.ThrowIfNull(config);
        
        Config = config;
        Name = config.Name ?? string.Empty;
        Description = config.Description ?? string.Empty;
        IsDefault = config.IsDefault;
        ServiceCount = config.ServiceNames?.Count ?? 0;
    }

    public void UpdateFromConfig()
    {
        Name = Config.Name ?? string.Empty;
        Description = Config.Description ?? string.Empty;
        IsDefault = Config.IsDefault;
        ServiceCount = Config.ServiceNames?.Count ?? 0;
    }

    public override string ToString() => Name;
}
