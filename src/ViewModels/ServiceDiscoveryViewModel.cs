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
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace AppPilot.ViewModels;

public partial class ServiceDiscoveryViewModel : ViewModelBase
{
    private readonly IServiceDiscoveryService _discoveryService;
    private readonly IConfigurationService _configService;
    private readonly IDialogService _dialogService;
    private readonly ILogger _logger;
    private readonly ObservableCollection<GroupConfig> _groups;

    [ObservableProperty]
    private bool _isDiscovering;

    [ObservableProperty]
    private string _discoveryPath = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Select a directory to discover .NET services";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAllTabChecked))]
    [NotifyPropertyChangedFor(nameof(IsWorkersTabChecked))]
    [NotifyPropertyChangedFor(nameof(IsGrpcTabChecked))]
    [NotifyPropertyChangedFor(nameof(IsWebApisTabChecked))]
    private int _selectedTypeTab;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private DiscoveredServiceItemViewModel? _selectedService;

    [ObservableProperty]
    private bool _showDetailsPanel;

    [ObservableProperty]
    private int _totalSelected;

    [ObservableProperty]
    private string _bulkGroupAssignment = string.Empty;

    [ObservableProperty]
    private string _bulkNewGroupName = string.Empty;

    [ObservableProperty]
    private string _bulkGroupStatus = string.Empty;

    public ObservableCollection<GroupConfig> Groups => _groups;

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

    public ServiceDiscoveryViewModel(
        IServiceDiscoveryService discoveryService,
        IConfigurationService configService,
        IDialogService dialogService,
        ILogger logger,
        ObservableCollection<GroupConfig> groups)
    {
        _discoveryService = discoveryService;
        _configService = configService;
        _dialogService = dialogService;
        _logger = logger;
        _groups = groups;

        AllServices.CollectionChanged += (s, e) =>
        {
            if (e.NewItems != null)
            {
                foreach (DiscoveredServiceItemViewModel item in e.NewItems)
                {
                    item.PropertyChanged += OnItemPropertyChanged;
                }
            }
            if (e.OldItems != null)
            {
                foreach (DiscoveredServiceItemViewModel item in e.OldItems)
                {
                    item.PropertyChanged -= OnItemPropertyChanged;
                }
            }
        };
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DiscoveredServiceItemViewModel.IsSelected))
        {
            TotalSelected = AllServices.Count(s => s.IsSelected);
        }
    }

    private void RecalculateSelected()
    {
        TotalSelected = AllServices.Count(s => s.IsSelected);
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
        TotalSelected = 0;
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
            RecalculateSelected();
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
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var service in FilteredServices)
        {
            service.IsSelected = false;
        }
    }

    [RelayCommand]
    private void EditService(DiscoveredServiceItemViewModel? item)
    {
        if (item == null) return;

        var config = item.ToManagedServiceConfig();
        var editorVm = new ServiceEditorViewModel(config, _groups);

        if (_dialogService.ShowServiceEditor(editorVm) != true)
        {
            return;
        }

        editorVm.ApplyTo(config);
        item.Service.DisplayName = config.DisplayName;
        item.Service.Type = config.Type;
        item.Service.ExecutablePath = config.ExecutablePath;
        item.Service.WorkingDirectory = config.WorkingDirectory;
        item.Service.CsprojPath = config.CsprojPath;
        item.Service.Port = config.Port;
        item.Service.HealthCheckUrl = config.HealthCheckUrl;
        item.Service.Arguments = config.Arguments;
        item.Service.EnvironmentVariables = new Dictionary<string, string>(config.Environment);
        item.Service.UseWindowsService = config.UseWindowsService;
        item.Service.Dependencies = new List<string>(config.Dependencies);
        item.Service.DisplayOrder = config.DisplayOrder ?? item.Service.DisplayOrder;
        item.Service.GroupId = config.GroupId;
        item.NotifyPropertiesChanged();

        ApplyFilters();
    }

    [RelayCommand]
    private void AssignGroupToSelected()
    {
        var targetGroupId = BulkGroupAssignment;
        if (string.IsNullOrEmpty(targetGroupId))
        {
            BulkGroupStatus = "Select a group first";
            return;
        }

        var count = 0;
        foreach (var service in FilteredServices.Where(s => s.IsSelected))
        {
            service.Service.GroupId = targetGroupId;
            service.NotifyPropertiesChanged();
            count++;
        }

        BulkGroupStatus = count > 0
            ? $"Assigned to {count} service(s)"
            : "No services selected";
    }

    [RelayCommand]
    private void AddNewGroupForBulk()
    {
        if (string.IsNullOrWhiteSpace(BulkNewGroupName))
        {
            BulkGroupStatus = "Enter a group name";
            return;
        }

        var trimmed = BulkNewGroupName.Trim();
        var existing = _groups.FirstOrDefault(g =>
            g.Id.Equals(trimmed, StringComparison.OrdinalIgnoreCase) ||
            g.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            BulkGroupAssignment = existing.Id;
            BulkNewGroupName = string.Empty;
            BulkGroupStatus = $"Group '{trimmed}' selected";
            return;
        }

        var maxOrder = _groups.Count > 0 ? _groups.Max(g => g.DisplayOrder) : 0;
        var newGroup = new GroupConfig
        {
            Id = trimmed,
            Name = trimmed,
            DisplayOrder = maxOrder + 1
        };
        _groups.Add(newGroup);
        BulkGroupAssignment = trimmed;
        BulkNewGroupName = string.Empty;
        BulkGroupStatus = $"Group '{trimmed}' created and selected";
    }

    [RelayCommand]
    private void ClearGroupFromSelected()
    {
        var count = 0;
        foreach (var service in FilteredServices.Where(s => s.IsSelected))
        {
            service.Service.GroupId = string.Empty;
            service.NotifyPropertiesChanged();
            count++;
        }

        BulkGroupStatus = count > 0
            ? $"Cleared group from {count} service(s)"
            : "No services selected";
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
                !service.DisplayName.Contains(searchLower!, StringComparison.OrdinalIgnoreCase) &&
                !service.ProjectName.Contains(searchLower!, StringComparison.OrdinalIgnoreCase) &&
                !service.ProjectPath.Contains(searchLower!, StringComparison.OrdinalIgnoreCase))
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
