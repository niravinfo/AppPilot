using AppPilot.Domain.Enums;
using AppPilot.Models;
using AppPilot.Services;
using AppPilot.Services.Configuration;
using AppPilot.Services.Discovery;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace AppPilot.ViewModels;

public partial class ServiceDiscoveryViewModel : ViewModelBase
{
    private readonly IServiceDiscoveryService _discoveryService;
    private readonly IConfigurationService _configService;
    private readonly ILogger _logger;

    [ObservableProperty]
    private bool _isDiscovering;

    [ObservableProperty]
    private string _discoveryPath = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Select a directory to discover .NET services";

    [ObservableProperty]
    private int _selectedTypeTab;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private DiscoveredServiceItemViewModel? _selectedService;

    [ObservableProperty]
    private bool _showDetailsPanel;

    public ObservableCollection<DiscoveredServiceItemViewModel> AllServices { get; } = [];
    public ObservableCollection<DiscoveredServiceItemViewModel> FilteredServices { get; } = [];

    public List<ServiceType> ServiceTypes { get; } = [ServiceType.Worker, ServiceType.Grpc, ServiceType.WebApi];

    public string GetTabLabel(ServiceType type) => type switch
    {
        ServiceType.Worker => "Workers",
        ServiceType.Grpc => "gRPC",
        ServiceType.WebApi => "Web APIs",
        _ => type.ToString()
    };

    public int GetTabCount(ServiceType type) => AllServices.Count(s => s.Type == type);
    public int TotalSelected => AllServices.Count(s => s.IsSelected);

    public ServiceDiscoveryViewModel(
        IServiceDiscoveryService discoveryService,
        IConfigurationService configService,
        ILogger logger)
    {
        _discoveryService = discoveryService;
        _configService = configService;
        _logger = logger;
    }

    [RelayCommand]
    private async Task DiscoverAsync()
    {
        if (string.IsNullOrWhiteSpace(DiscoveryPath))
        {
            StatusMessage = "Please select a directory first";
            return;
        }

        IsDiscovering = true;
        StatusMessage = "Discovering services...";
        AllServices.Clear();
        SelectedService = null;
        ShowDetailsPanel = false;

        try
        {
            var discovered = await _discoveryService.DiscoverAsync(DiscoveryPath);

            foreach (var service in discovered)
            {
                AllServices.Add(new DiscoveredServiceItemViewModel(service));
            }

            if (discovered.Count == 0)
            {
                StatusMessage = "No services found in the selected directory";
            }
            else
            {
                StatusMessage = $"Found {discovered.Count} service(s) — {GetTabCount(ServiceType.Worker)} workers, {GetTabCount(ServiceType.Grpc)} gRPC, {GetTabCount(ServiceType.WebApi)} APIs";
            }

            ApplyFilters();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Service discovery failed");
            StatusMessage = $"Discovery failed: {ex.Message}";
        }
        finally
        {
            IsDiscovering = false;
        }
    }

    [RelayCommand]
    private void BrowseDirectory()
    {
        var dialog = new OpenFolderDialog { Title = "Select Root Directory" };
        if (dialog.ShowDialog() == true)
        {
            DiscoveryPath = dialog.FolderName;
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var service in FilteredServices)
        {
            service.IsSelected = true;
        }
        OnPropertyChanged(nameof(TotalSelected));
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var service in FilteredServices)
        {
            service.IsSelected = false;
        }
        OnPropertyChanged(nameof(TotalSelected));
    }

    [RelayCommand]
    private void ToggleSelectAll()
    {
        var allSelected = FilteredServices.All(s => s.IsSelected);
        foreach (var service in FilteredServices)
        {
            service.IsSelected = !allSelected;
        }
        OnPropertyChanged(nameof(TotalSelected));
    }

    [RelayCommand]
    private void FilterByAll() => SelectedTypeTab = 0;

    [RelayCommand]
    private void FilterByWorkers() => SelectedTypeTab = 1;

    [RelayCommand]
    private void FilterByGrpc() => SelectedTypeTab = 2;

    [RelayCommand]
    private void FilterByWebApis() => SelectedTypeTab = 3;

    partial void OnSelectedTypeTabChanged(int value) => ApplyFilters();

    partial void OnSearchTextChanged(string value) => ApplyFilters();

    partial void OnSelectedServiceChanged(DiscoveredServiceItemViewModel? value)
    {
        ShowDetailsPanel = value != null;
    }

    public bool IsAllTabChecked => SelectedTypeTab == 0;
    public bool IsWorkersTabChecked => SelectedTypeTab == 1;
    public bool IsGrpcTabChecked => SelectedTypeTab == 2;
    public bool IsWebApisTabChecked => SelectedTypeTab == 3;

    private void ApplyFilters()
    {
        FilteredServices.Clear();

        ServiceType? typeFilter = SelectedTypeTab > 0 ? ServiceTypes[SelectedTypeTab - 1] : null;
        var searchLower = SearchText?.ToLowerInvariant();
        var hasSearch = !string.IsNullOrWhiteSpace(searchLower);

        foreach (var service in AllServices)
        {
            if (typeFilter.HasValue && service.Type != typeFilter.Value)
                continue;

            if (hasSearch &&
                !service.DisplayName.Contains(searchLower!, System.StringComparison.OrdinalIgnoreCase) &&
                !service.ProjectName.Contains(searchLower!, System.StringComparison.OrdinalIgnoreCase) &&
                !service.ProjectPath.Contains(searchLower!, System.StringComparison.OrdinalIgnoreCase))
                continue;

            FilteredServices.Add(service);
        }
    }

    public List<ManagedServiceConfig> GetSelectedConfigs()
    {
        var existingNames = _configService.Load().Services.Select(s => s.Name).ToHashSet();
        var selected = AllServices.Where(s => s.IsSelected).ToList();
        var configs = new List<ManagedServiceConfig>();

        foreach (var service in selected)
        {
            var config = service.ToManagedServiceConfig();

            if (existingNames.Contains(config.Name))
            {
                var suffix = 1;
                var originalName = config.Name;
                while (existingNames.Contains(config.Name))
                {
                    config.Name = $"{originalName}_{suffix++}";
                    config.DisplayName = $"{service.DisplayName} ({suffix})";
                }
            }

            configs.Add(config);
            existingNames.Add(config.Name);
        }

        return configs;
    }
}
