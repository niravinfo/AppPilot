using AppPilot.Domain.Enums;
using AppPilot.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AppPilot.ViewModels;

/// <summary>
/// ViewModel for editing a profile.
/// </summary>
public partial class ProfileEditorViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private bool _isDefault;

    [ObservableProperty]
    private int _displayOrder = 999;

    [ObservableProperty]
    private string _serviceSearchText = string.Empty;

    [ObservableProperty]
    private ProfileServiceItemViewModel? _selectedAvailableService;

    [ObservableProperty]
    private ProfileServiceItemViewModel? _selectedProfileService;

    /// <summary>
    /// All available services that can be added to the profile.
    /// </summary>
    public ObservableCollection<ProfileServiceItemViewModel> AvailableServices { get; } = [];

    /// <summary>
    /// Filtered available services based on search.
    /// </summary>
    public ObservableCollection<ProfileServiceItemViewModel> FilteredAvailableServices { get; } = [];

    /// <summary>
    /// Services currently in the profile (in display order).
    /// </summary>
    public ObservableCollection<ProfileServiceItemViewModel> ProfileServices { get; } = [];

    public bool IsNew { get; }
    public string Title => IsNew ? "Create Profile" : $"Edit Profile — {Name}";
    public string SaveButtonText => IsNew ? "Create Profile" : "Save Changes";

    private readonly string? _originalId;

    /// <summary>
    /// Creates a new profile editor for adding a new profile.
    /// </summary>
    public ProfileEditorViewModel(IEnumerable<ManagedServiceConfig> allServices)
    {
        IsNew = true;
        _originalId = null;
        InitializeAvailableServices(allServices, []);
        UpdateFilteredServices();
    }

    /// <summary>
    /// Creates a profile editor for editing an existing profile.
    /// </summary>
    public ProfileEditorViewModel(ProfileConfig config, IEnumerable<ManagedServiceConfig> allServices)
    {
        IsNew = false;
        _originalId = config.Id;
        _name = config.Name;
        _description = config.Description;
        _isDefault = config.IsDefault;
        _displayOrder = config.DisplayOrder;

        InitializeAvailableServices(allServices, config.ServiceNames);
        UpdateFilteredServices();
    }

    private void InitializeAvailableServices(IEnumerable<ManagedServiceConfig> allServices, IReadOnlyList<string> profileServiceNames)
    {
        var profileServiceSet = new HashSet<string>(profileServiceNames, StringComparer.OrdinalIgnoreCase);
        var serviceOrder = profileServiceNames
            .Select((name, index) => (name, index))
            .ToDictionary(x => x.name, x => x.index, StringComparer.OrdinalIgnoreCase);

        foreach (var service in allServices.OrderBy(s => s.DisplayOrder ?? 999).ThenBy(s => s.DisplayName))
        {
            var item = new ProfileServiceItemViewModel(service);

            if (profileServiceSet.Contains(service.Name))
            {
                // Add to profile services in the original order
                ProfileServices.Add(item);
            }
            else
            {
                AvailableServices.Add(item);
            }
        }

        // Sort profile services by the original order from config
        var sortedProfileServices = ProfileServices
            .OrderBy(s => serviceOrder.TryGetValue(s.ServiceName, out var order) ? order : int.MaxValue)
            .ToList();
        ProfileServices.Clear();
        foreach (var s in sortedProfileServices)
        {
            ProfileServices.Add(s);
        }
    }

    partial void OnServiceSearchTextChanged(string value)
    {
        UpdateFilteredServices();
    }

    private void UpdateFilteredServices()
    {
        FilteredAvailableServices.Clear();

        var searchLower = ServiceSearchText?.ToLowerInvariant();
        var hasSearch = !string.IsNullOrWhiteSpace(searchLower);

        foreach (var service in AvailableServices)
        {
            if (!hasSearch ||
                service.DisplayName.Contains(searchLower!, StringComparison.OrdinalIgnoreCase) ||
                service.TypeName.Contains(searchLower!, StringComparison.OrdinalIgnoreCase) ||
                service.GroupName.Contains(searchLower!, StringComparison.OrdinalIgnoreCase))
            {
                FilteredAvailableServices.Add(service);
            }
        }
    }

    [RelayCommand]
    private void AddService()
    {
        if (SelectedAvailableService == null)
            return;

        var service = SelectedAvailableService;
        AvailableServices.Remove(service);
        FilteredAvailableServices.Remove(service);
        ProfileServices.Add(service);
        SelectedAvailableService = null;
    }

    [RelayCommand]
    private void AddAllFilteredServices()
    {
        var toAdd = FilteredAvailableServices.ToList();
        foreach (var service in toAdd)
        {
            AvailableServices.Remove(service);
            FilteredAvailableServices.Remove(service);
            ProfileServices.Add(service);
        }
    }

    [RelayCommand]
    private void RemoveService()
    {
        if (SelectedProfileService == null)
            return;

        var service = SelectedProfileService;
        ProfileServices.Remove(service);
        
        // Add back to available services in proper order
        var insertIndex = 0;
        foreach (var existing in AvailableServices)
        {
            if ((service.DisplayOrder ?? 999) < (existing.DisplayOrder ?? 999) ||
                ((service.DisplayOrder ?? 999) == (existing.DisplayOrder ?? 999) &&
                 string.Compare(service.DisplayName, existing.DisplayName, StringComparison.OrdinalIgnoreCase) < 0))
            {
                break;
            }
            insertIndex++;
        }
        AvailableServices.Insert(insertIndex, service);
        UpdateFilteredServices();
        SelectedProfileService = null;
    }

    [RelayCommand]
    private void RemoveAllServices()
    {
        var toRemove = ProfileServices.ToList();
        foreach (var service in toRemove)
        {
            ProfileServices.Remove(service);
            
            var insertIndex = 0;
            foreach (var existing in AvailableServices)
            {
                if ((service.DisplayOrder ?? 999) < (existing.DisplayOrder ?? 999) ||
                    ((service.DisplayOrder ?? 999) == (existing.DisplayOrder ?? 999) &&
                     string.Compare(service.DisplayName, existing.DisplayName, StringComparison.OrdinalIgnoreCase) < 0))
                {
                    break;
                }
                insertIndex++;
            }
            AvailableServices.Insert(insertIndex, service);
        }
        UpdateFilteredServices();
    }

    [RelayCommand]
    private void MoveServiceUp()
    {
        if (SelectedProfileService == null)
            return;

        var index = ProfileServices.IndexOf(SelectedProfileService);
        if (index > 0)
        {
            ProfileServices.Move(index, index - 1);
        }
    }

    [RelayCommand]
    private void MoveServiceDown()
    {
        if (SelectedProfileService == null)
            return;

        var index = ProfileServices.IndexOf(SelectedProfileService);
        if (index >= 0 && index < ProfileServices.Count - 1)
        {
            ProfileServices.Move(index, index + 1);
        }
    }

    public bool CanSave => !string.IsNullOrWhiteSpace(Name);

    public void ApplyTo(ProfileConfig config)
    {
        config.Name = Name.Trim();
        config.Description = Description?.Trim() ?? string.Empty;
        config.IsDefault = IsDefault;
        config.DisplayOrder = DisplayOrder;
        config.ServiceNames = ProfileServices.Select(s => s.ServiceName).ToList();
    }

    public ProfileConfig ToConfig()
    {
        var config = new ProfileConfig
        {
            Id = _originalId ?? Guid.NewGuid().ToString("N")[..8]
        };
        ApplyTo(config);
        return config;
    }
}

/// <summary>
/// ViewModel for a service item within the profile editor.
/// </summary>
public partial class ProfileServiceItemViewModel : ViewModelBase
{
    public ManagedServiceConfig Config { get; }

    public string ServiceName => Config.Name;
    public string DisplayName => Config.DisplayName;
    public string TypeName => Config.Type.ToString();
    public int? DisplayOrder => Config.DisplayOrder;
    public ServiceType ServiceType => Config.Type;
    public string GroupName => Config.GroupId;

    /// <summary>
    /// Whether this is a NodeApp service (cannot be auto-started/stopped).
    /// </summary>
    public bool IsNodeApp => Config.Type == ServiceType.NodeApp;

    public ProfileServiceItemViewModel(ManagedServiceConfig config)
    {
        Config = config;
    }

    public override string ToString() => DisplayName;
}
