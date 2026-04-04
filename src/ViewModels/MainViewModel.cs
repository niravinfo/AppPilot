using AppPilot.Domain.Enums;
using AppPilot.Models;
using AppPilot.Services;
using AppPilot.Services.Build;
using AppPilot.Services.Configuration;
using AppPilot.Services.Discovery;
using AppPilot.Services.Git;
using AppPilot.Services.HealthCheck;
using AppPilot.Services.ServiceControl;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace AppPilot.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    internal List<GroupConfig> _serviceGroups = [];
    internal Dictionary<string, GroupConfig> _groupDict = [];
    internal readonly IConfigurationService _configService;
    private readonly Dictionary<string, ServiceGroupViewModel> _groupViewModelCache = [];
    internal IConfigurationService ConfigService => _configService;
    private readonly IServiceController _windowsServiceController;
    private readonly IProcessService _processService;
    private readonly IHealthChecker _healthChecker;
    private readonly ILogger<MainViewModel> _logger;
    private readonly IDialogService _dialogService;
    private readonly IBuildService _buildService;
    private readonly IGitService _gitService;
    private readonly IServiceDiscoveryService _discoveryService;
    private readonly DispatcherTimer _pollingTimer;

    [ObservableProperty]
    private ObservableCollection<ServiceItemViewModel> _services = new();

    [ObservableProperty]
    private bool _isLightTheme = ThemeManager.IsLight;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _lastUpdateTime = "-";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _searchText = string.Empty;


    [ObservableProperty]
    private ObservableCollection<ServiceGroupViewModel> _filteredGroups = new();

    [ObservableProperty]
    private ObservableCollection<GitRepositoryViewModel> _filteredGitRepositories = new();

    [ObservableProperty]
    private int _selectedTab;

    [ObservableProperty]
    private ObservableCollection<GitRepositoryViewModel> _gitRepositories = new();

    private List<ManagedServiceConfig> _serviceConfigs = new();
    private List<GitRepositoryConfig> _gitRepositoryConfigs = new();

    public MainViewModel(
        IConfigurationService configService,
        IServiceController windowsServiceController,
        IProcessService processService,
        IHealthChecker healthChecker,
        ILogger<MainViewModel> logger,
        IDialogService dialogService,
        IBuildService buildService,
        IGitService gitService,
        IServiceDiscoveryService discoveryService)
    {
        _configService = configService;
        _windowsServiceController = windowsServiceController;
        _processService = processService;
        _healthChecker = healthChecker;
        _logger = logger;
        _dialogService = dialogService;
        _buildService = buildService;
        _gitService = gitService;
        _discoveryService = discoveryService;

        _pollingTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _pollingTimer.Tick += async (s, e) => await RefreshStatusAsync();
    }

    public void Initialize()
    {
        LoadConfiguration();
        _pollingTimer.Start();
        _ = RefreshStatusAsync();
    }

    private void LoadConfiguration()
    {
        var settings = _configService.Load();
        _serviceConfigs = settings.Services;
        _gitRepositoryConfigs = settings.GitRepositories;
        _serviceGroups = settings.Groups ?? [];
        _groupDict = _serviceGroups.ToDictionary(g => g.Id);

        if (settings.AppPilot.PollingIntervalMs > 0)
        {
            _pollingTimer.Interval = TimeSpan.FromMilliseconds(settings.AppPilot.PollingIntervalMs);
        }

        Services.Clear();

        // Optimize: Calculate group info once, reuse for all services in same group
        var groupInfoCache = new Dictionary<string, GroupInfo>();

        foreach (var config in _serviceConfigs
            .OrderBy(s => s.DisplayOrder ?? 999)
            .ThenBy(s => s.Name))
        {
            GroupInfo groupInfo;
            var groupKey = config.GroupId ?? string.Empty;

            if (!groupInfoCache.TryGetValue(groupKey, out groupInfo!))
            {
                groupInfo = string.IsNullOrWhiteSpace(config.GroupId)
                    ? GroupInfo.Empty
                    : (_groupDict.TryGetValue(config.GroupId, out var group)
                        ? GroupInfo.FromConfig(group)
                        : new GroupInfo { Id = config.GroupId, Name = config.GroupId });
                groupInfoCache[groupKey] = groupInfo;
            }

            Services.Add(new ServiceItemViewModel(
                config,
                groupInfo,
                _windowsServiceController,
                _processService,
                _buildService,
                _logger,
                editCallback: EditService,
                deleteCallback: DeleteService));
        }

        // Load Git repositories and link services
        GitRepositories.Clear();

        // Optimize: Build service lookup dictionary to avoid repeated FirstOrDefault
        var serviceLookup = new Dictionary<string, ServiceItemViewModel>();
        foreach (var svc in Services)
        {
            serviceLookup[svc.Config.Name] = svc;
        }

        foreach (var repoConfig in _gitRepositoryConfigs)
        {
            var repoVm = new GitRepositoryViewModel(repoConfig, _buildService, _gitService, _logger, this);
            foreach (var name in repoConfig.LinkedServiceNames)
            {
                if (serviceLookup.TryGetValue(name, out var svc))
                {
                    repoVm.LinkedServices.Add(svc);
                }
            }

            GitRepositories.Add(repoVm);
        }

        // Initialise git info in background (non-blocking)
        // Optimize: Avoid LINQ Select allocation
        var gitInitTasks = new Task[GitRepositories.Count];
        for (int i = 0; i < GitRepositories.Count; i++)
        {
            gitInitTasks[i] = GitRepositories[i].InitializeAsync();
        }

        _ = Task.WhenAll(gitInitTasks);

        RebuildFilteredGroups();
        RebuildFilteredGitRepositories();
        StatusText = $"Loaded {_serviceConfigs.Count} services";
    }

    [RelayCommand]
    private void Refresh()
    {
        _ = RefreshStatusAsync();
    }

    private async Task RefreshStatusAsync()
    {
        // Optimize: Avoid LINQ Select allocation - build task array directly
        var tasks = new Task[Services.Count];
        for (int i = 0; i < Services.Count; i++)
        {
            tasks[i] = UpdateServiceStatusAsync(Services[i]);
        }

        await Task.WhenAll(tasks);
        LastUpdateTime = DateTime.Now.ToString("HH:mm:ss");
    }

    private async Task UpdateServiceStatusAsync(ServiceItemViewModel service)
    {
        try
        {
            var config = service.Config;
            ServiceStatus status;
            string? healthError = null;

            if (config.Type == ServiceType.Worker && config.UseWindowsService)
            {
                status = _windowsServiceController.GetStatus(config);
            }
            else
            {
                status = _processService.GetStatus(config);

                // Keep ProcessId in sync with the live process so Stop works correctly
                // even when AppPilot was restarted and the process was already running.
                if (status == ServiceStatus.Running)
                {
                    service.ProcessId ??= _processService.GetProcessId(config);
                }
                else if (status == ServiceStatus.Stopped)
                {
                    service.ProcessId = null;
                }

                if (status == ServiceStatus.Running && !string.IsNullOrEmpty(config.HealthCheckUrl))
                {
                    healthError = await _healthChecker.CheckHealthAsync(config.HealthCheckUrl);
                    if (healthError != null)
                    {
                        status = ServiceStatus.Error;
                    }
                }
            }

            service.Status = status;
            service.LastChecked = DateTime.Now;

            // Set the real error when health check fails, clear it when healthy,
            // and leave it untouched when Stopped so the last user-action error stays visible.
            if (healthError != null)
            {
                service.ErrorMessage = healthError;
            }
            else if (status == ServiceStatus.Running)
            {
                service.ErrorMessage = string.Empty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status for {Name}", service.Config.Name);
            service.Status = ServiceStatus.Error;
            service.ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task StartAllAsync()
    {
        IsLoading = true;
        StatusText = "Starting all services...";

        try
        {
            // Optimize: Use List and manual sort to avoid LINQ allocations
            var orderedServices = new List<ServiceItemViewModel>();
            foreach (var s in Services)
            {
                if (s.Status != ServiceStatus.Running)
                {
                    orderedServices.Add(s);
                }
            }

            orderedServices.Sort((a, b) =>
                (a.Config.DisplayOrder ?? 999).CompareTo(b.Config.DisplayOrder ?? 999) != 0
                    ? (a.Config.DisplayOrder ?? 999).CompareTo(b.Config.DisplayOrder ?? 999)
                    : string.Compare(a.Config.Name, b.Config.Name, StringComparison.OrdinalIgnoreCase));

            foreach (var service in orderedServices)
            {
                await service.StartAsync();
                await Task.Delay(500);
            }

            StatusText = "All services started";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task StopAllAsync()
    {
        IsLoading = true;
        StatusText = "Stopping all services...";

        try
        {
            // Optimize: Use List and manual sort to avoid LINQ allocations
            var orderedServices = new List<ServiceItemViewModel>();
            foreach (var s in Services)
            {
                if (s.Status == ServiceStatus.Running)
                {
                    orderedServices.Add(s);
                }
            }

            orderedServices.Sort((a, b) =>
                (b.Config.DisplayOrder ?? 999).CompareTo(a.Config.DisplayOrder ?? 999) != 0
                    ? (b.Config.DisplayOrder ?? 999).CompareTo(a.Config.DisplayOrder ?? 999)
                    : string.Compare(b.Config.Name, a.Config.Name, StringComparison.OrdinalIgnoreCase));

            foreach (var service in orderedServices)
            {
                await service.StopAsync();
                await Task.Delay(500);
            }

            StatusText = "All services stopped";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task StartServiceAsync(ServiceItemViewModel service)
    {
        await service.StartAsync();
    }

    public event Action? FocusSearchRequested;

    [RelayCommand]
    public void FocusSearch()
    {
        FocusSearchRequested?.Invoke();
    }

    public async Task StopServiceAsync(ServiceItemViewModel service)
    {
        await service.StopAsync();
    }

    partial void OnSearchTextChanged(string value)
    {
        RebuildFilteredGroups();
        RebuildFilteredGitRepositories();
    }

    partial void OnSelectedTabChanged(int value)
    {
        RebuildFilteredGitRepositories();
    }

    private void RebuildFilteredGroups()
    {
        FilteredGroups.Clear();

        // Optimize: Use ReadOnlySpan for string comparison where possible
        var searchLower = SearchText?.ToLowerInvariant();
        var hasSearch = !string.IsNullOrWhiteSpace(searchLower);

        // Optimize: Single pass filtering and grouping
        var groupedServices = new Dictionary<string, List<ServiceItemViewModel>>();
        var groupConfigs = new Dictionary<string, GroupConfig>();

        foreach (var svc in Services)
        {
            // Optimize: Skip expensive Contains() calls if no search
            if (hasSearch)
            {
                var matches = svc.DisplayName.Contains(searchLower!, StringComparison.OrdinalIgnoreCase) ||
                              svc.TypeName.Contains(searchLower!, StringComparison.OrdinalIgnoreCase);

                if (!matches && _groupDict.TryGetValue(svc.Config.GroupId, out var group))
                {
                    matches = group.Name.Contains(searchLower!, StringComparison.OrdinalIgnoreCase);
                }

                if (!matches) continue;
            }

            var groupId = string.IsNullOrWhiteSpace(svc.Config.GroupId) ? "__ungrouped__" : svc.Config.GroupId;

            if (!groupedServices.TryGetValue(groupId, out var list))
            {
                list = new List<ServiceItemViewModel>();
                groupedServices[groupId] = list;

                if (!groupConfigs.ContainsKey(groupId))
                {
                    groupConfigs[groupId] = _groupDict.TryGetValue(groupId, out var cfg)
                        ? cfg
                        : new GroupConfig { Id = groupId, Name = groupId, DisplayOrder = 9999 };
                }
            }

            list.Add(svc);
        }

        var showHeaders = groupedServices.Count > 1 ||
                         (groupedServices.Count == 1 && !groupedServices.ContainsKey("__ungrouped__"));

        // Optimize: Sort groups without LINQ allocations
        var sortedGroups = groupConfigs.Values
            .OrderBy(g => g.DisplayOrder)
            .ThenBy(g => g.Name);

        foreach (var groupConfig in sortedGroups)
        {
            if (!groupedServices.TryGetValue(groupConfig.Id, out var services))
                continue;

            // Optimize: Reuse cached ServiceGroupViewModel if possible
            if (!_groupViewModelCache.TryGetValue(groupConfig.Id, out var groupVm))
            {
                groupVm = new ServiceGroupViewModel(groupConfig);
                _groupViewModelCache[groupConfig.Id] = groupVm;
            }

            groupVm.ShowHeader = showHeaders;
            groupVm.Items.Clear();

            foreach (var svc in services)
            {
                groupVm.Items.Add(svc);
            }

            FilteredGroups.Add(groupVm);
        }

        RebuildFilteredGitRepositories();
    }

    private void RebuildFilteredGitRepositories()
    {
        FilteredGitRepositories.Clear();
        if (SelectedTab != 1)
        {
            return;
        }

        var searchLower = SearchText?.ToLowerInvariant();
        var hasSearch = !string.IsNullOrWhiteSpace(searchLower);

        // Optimize: Avoid LINQ Where allocation when no filter
        if (!hasSearch)
        {
            foreach (var repo in GitRepositories)
            {
                FilteredGitRepositories.Add(repo);
            }
        }
        else
        {
            foreach (var repo in GitRepositories)
            {
                if (repo.Name.Contains(searchLower!, StringComparison.OrdinalIgnoreCase))
                {
                    FilteredGitRepositories.Add(repo);
                }
            }
        }
    }

    [RelayCommand]
    private void AddService()
    {
        var editorVm = new ServiceEditorViewModel(_serviceGroups);
        if (_dialogService.ShowServiceEditor(editorVm) != true)
        {
            return;
        }

        var config = editorVm.ToConfig();
        _serviceConfigs.Add(config);

        var groupInfo = string.IsNullOrWhiteSpace(config.GroupId)
            ? GroupInfo.Empty
            : (_groupDict.TryGetValue(config.GroupId, out var group)
                ? GroupInfo.FromConfig(group)
                : new GroupInfo { Id = config.GroupId, Name = config.GroupId });

        Services.Add(new ServiceItemViewModel(
            config,
            groupInfo,
            _windowsServiceController,
            _processService,
            _buildService,
            _logger,
            editCallback: EditService,
            deleteCallback: DeleteService));

        RebuildFilteredGroups();
        SaveConfiguration();
        StatusText = $"Service '{config.DisplayName}' added";
    }

    [RelayCommand]
    private async Task DiscoverServicesAsync()
    {
        var loggerFactory = App.Services.GetService(typeof(Microsoft.Extensions.Logging.ILoggerFactory)) as Microsoft.Extensions.Logging.ILoggerFactory;
        var logger = loggerFactory?.CreateLogger<ServiceDiscoveryViewModel>() ?? (Microsoft.Extensions.Logging.ILogger)Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        var discoveryVm = new ServiceDiscoveryViewModel(_discoveryService, _configService, logger);

        if (_configService.Load().AppPilot.BasePath is string basePath && !string.IsNullOrEmpty(basePath))
        {
            discoveryVm.DiscoveryPath = basePath;
        }

        if (_dialogService.ShowServiceDiscovery(discoveryVm) != true)
        {
            return;
        }

        var configs = discoveryVm.GetSelectedConfigs();
        if (configs.Count == 0)
        {
            return;
        }

        foreach (var config in configs)
        {
            _serviceConfigs.Add(config);

            var groupInfo = string.IsNullOrWhiteSpace(config.GroupId)
                ? GroupInfo.Empty
                : (_groupDict.TryGetValue(config.GroupId, out var group)
                    ? GroupInfo.FromConfig(group)
                    : new GroupInfo { Id = config.GroupId, Name = config.GroupId });

            Services.Add(new ServiceItemViewModel(
                config,
                groupInfo,
                _windowsServiceController,
                _processService,
                _buildService,
                _logger,
                editCallback: EditService,
                deleteCallback: DeleteService));
        }

        RebuildFilteredGroups();
        SaveConfiguration();
        StatusText = $"Imported {configs.Count} service(s)";
    }

    public void EditService(ServiceItemViewModel serviceVm)
    {
        var editorVm = new ServiceEditorViewModel(serviceVm.Config, _serviceGroups);
        if (_dialogService.ShowServiceEditor(editorVm) != true)
        {
            return;
        }

        editorVm.ApplyTo(serviceVm.Config);
        serviceVm.NotifyDisplayPropertiesChanged();
        serviceVm.RefreshColors();
        RebuildFilteredGroups();
        SaveConfiguration();
        StatusText = $"Service '{serviceVm.Config.DisplayName}' updated";
    }

    public void DeleteService(ServiceItemViewModel serviceVm)
    {
        if (!_dialogService.Confirm(
            $"Remove '{serviceVm.DisplayName}' from AppPilot?\n\nThis will not stop or uninstall the service.",
            "Remove Service"))
        {
            return;
        }

        _serviceConfigs.Remove(serviceVm.Config);
        Services.Remove(serviceVm);
        RebuildFilteredGroups();
        SaveConfiguration();
        StatusText = $"Service '{serviceVm.DisplayName}' removed";
    }

    private void SaveConfiguration()
    {
        var settings = _configService.Load();
        settings.Services = _serviceConfigs;
        settings.GitRepositories = _gitRepositoryConfigs;
        _configService.Save(settings);
    }

    [RelayCommand]
    private void AddGitRepository()
    {
        var editorVm = new GitRepositoryEditorViewModel();
        if (_dialogService.ShowGitRepositoryEditor(editorVm) != true)
        {
            return;
        }

        var config = editorVm.ToConfig();
        _gitRepositoryConfigs.Add(config);
        var repoVm = new GitRepositoryViewModel(config, _buildService, _gitService, _logger, this);

        foreach (var name in config.LinkedServiceNames)
        {
            var svc = Services.FirstOrDefault(s => s.Config.Name == name);
            if (svc is not null)
            {
                repoVm.LinkedServices.Add(svc);
            }
        }

        GitRepositories.Add(repoVm);
        _ = repoVm.InitializeAsync();
        SaveConfiguration();
        StatusText = $"Repository '{config.DisplayName}' added";
    }

    public void EditGitRepository(GitRepositoryViewModel repoVm)
    {
        var editorVm = new GitRepositoryEditorViewModel(repoVm.Config);
        if (_dialogService.ShowGitRepositoryEditor(editorVm) != true)
        {
            return;
        }

        editorVm.ApplyTo(repoVm.Config);

        repoVm.LinkedServices.Clear();
        foreach (var name in repoVm.Config.LinkedServiceNames)
        {
            var svc = Services.FirstOrDefault(s => s.Config.Name == name);
            if (svc is not null)
            {
                repoVm.LinkedServices.Add(svc);
            }
        }

        SaveConfiguration();
        StatusText = $"Repository '{repoVm.Config.DisplayName}' updated";
    }

    public void DeleteGitRepository(GitRepositoryViewModel repoVm)
    {
        if (!_dialogService.Confirm(
            $"Remove '{repoVm.Name}' from AppPilot?\n\nThis will not delete the local repository.",
            "Remove Repository"))
        {
            return;
        }

        _gitRepositoryConfigs.Remove(repoVm.Config);
        GitRepositories.Remove(repoVm);
        SaveConfiguration();
        StatusText = $"Repository '{repoVm.Name}' removed";
    }

    [RelayCommand]
    private void SelectServicesTab() => SelectedTab = 0;

    [RelayCommand]
    private void SelectGitTab() => SelectedTab = 1;

    [RelayCommand]
    private void ToggleTheme()
    {
        ThemeManager.Toggle();
        IsLightTheme = ThemeManager.IsLight;
        RefreshServiceColors();
    }

    private void RefreshServiceColors()
    {
        foreach (var service in Services)
        {
            service.RefreshColors();
        }

        RebuildFilteredGroups();
    }

    public void Shutdown()
    {
        _pollingTimer.Stop();
    }

    [RelayCommand]
    private void CloseApp()
    {
        Shutdown();
        System.Windows.Application.Current.Shutdown();
    }
}
