using AppPilot.Domain.Enums;
using AppPilot.Models;
using AppPilot.Services;
using System.Windows.Media;
using AppPilot.Services.Build;
using AppPilot.Services.Configuration;
using AppPilot.Services.Git;
using AppPilot.Services.HealthCheck;
using AppPilot.Services.ServiceControl;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace AppPilot.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IConfigurationService _configService;
    private readonly IServiceController _windowsServiceController;
    private readonly IProcessService _processService;
    private readonly IHealthChecker _healthChecker;
    private readonly ILogger _logger;
    private readonly IDialogService _dialogService;
    private readonly IBuildService _buildService;
    private readonly IGitService _gitService;
    private readonly DispatcherTimer _pollingTimer;

    [ObservableProperty]
    private ObservableCollection<ServiceItemViewModel> _services = new();

    [ObservableProperty]
    private ObservableCollection<ServiceGroupViewModel> _groups = new();

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
    private int _selectedTab;

    [ObservableProperty]
    private ObservableCollection<GitRepositoryViewModel> _gitRepositories = new();

    private List<ManagedServiceConfig> _serviceConfigs = new();

    public MainViewModel(
        IConfigurationService configService,
        IServiceController windowsServiceController,
        IProcessService processService,
        IHealthChecker healthChecker,
        ILogger logger,
        IDialogService dialogService,
        IBuildService buildService,
        IGitService gitService)
    {
        _configService = configService;
        _windowsServiceController = windowsServiceController;
        _processService = processService;
        _healthChecker = healthChecker;
        _logger = logger;
        _dialogService = dialogService;
        _buildService = buildService;
        _gitService = gitService;

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

        if (settings.AppPilot.PollingIntervalMs > 0)
            _pollingTimer.Interval = TimeSpan.FromMilliseconds(settings.AppPilot.PollingIntervalMs);

        Services.Clear();
        foreach (var config in _serviceConfigs.OrderBy(s => s.StartOrder))
            Services.Add(new ServiceItemViewModel(config, _windowsServiceController, _processService, _buildService, _logger, this));

        // Load Git repositories and link services
        GitRepositories.Clear();
        foreach (var repoConfig in settings.GitRepositories)
        {
            var repoVm = new GitRepositoryViewModel(repoConfig, _buildService, _gitService, _logger);
            foreach (var name in repoConfig.LinkedServiceNames)
            {
                var svc = Services.FirstOrDefault(s => s.Config.Name == name);
                if (svc is not null)
                    repoVm.LinkedServices.Add(svc);
            }
            GitRepositories.Add(repoVm);
        }

        // Initialise git info in background (non-blocking)
        _ = Task.WhenAll(GitRepositories.Select(r => r.InitializeAsync()));

        RebuildGroups();
        StatusText = $"Loaded {_serviceConfigs.Count} services";
    }

    [RelayCommand]
    private void Refresh()
    {
        _ = RefreshStatusAsync();
    }

    private async Task RefreshStatusAsync()
    {
        await Task.WhenAll(Services.Select(UpdateServiceStatusAsync));
        LastUpdateTime = DateTime.Now.ToString("HH:mm:ss");
    }

    private async Task UpdateServiceStatusAsync(ServiceItemViewModel service)
    {
        try
        {
            var config = service.Config;
            ServiceStatus status;
            string? healthError = null;

            if (config.Type == ServiceType.Worker)
            {
                status = _windowsServiceController.GetStatus(config);
            }
            else
            {
                status = _processService.GetStatus(config);

                // Keep ProcessId in sync with the live process so Stop works correctly
                // even when AppPilot was restarted and the process was already running.
                if (status == ServiceStatus.Running)
                    service.ProcessId ??= _processService.GetProcessId(config);
                else if (status == ServiceStatus.Stopped)
                    service.ProcessId = null;

                if (status == ServiceStatus.Running && !string.IsNullOrEmpty(config.HealthCheckUrl))
                {
                    healthError = await _healthChecker.CheckHealthAsync(config.HealthCheckUrl);
                    if (healthError != null)
                        status = ServiceStatus.Error;
                }
            }

            service.Status = status;
            service.LastChecked = DateTime.Now;

            // Set the real error when health check fails, clear it when healthy,
            // and leave it untouched when Stopped so the last user-action error stays visible.
            if (healthError != null)
                service.ErrorMessage = healthError;
            else if (status == ServiceStatus.Running)
                service.ErrorMessage = string.Empty;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error updating status for {Name}", service.Config.Name);
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
            var orderedServices = Services
                .Where(s => s.Config.AutoStart || s.Status != ServiceStatus.Running)
                .OrderBy(s => s.Config.StartOrder)
                .ToList();

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
            var orderedServices = Services
                .Where(s => s.Status == ServiceStatus.Running)
                .OrderByDescending(s => s.Config.StartOrder)
                .ToList();

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

    public async Task StopServiceAsync(ServiceItemViewModel service)
    {
        await service.StopAsync();
    }

    private void RebuildGroups()
    {
        Groups.Clear();
        var grouped = Services
            .GroupBy(s => string.IsNullOrWhiteSpace(s.Config.GroupName) ? "General" : s.Config.GroupName)
            .OrderBy(g => g.Key)
            .ToList();

        var showHeaders = grouped.Count > 1 || (grouped.Count == 1 && grouped[0].Key != "General");

        foreach (var g in grouped)
        {
            var group = new ServiceGroupViewModel(g.Key) { ShowHeader = showHeaders };
            foreach (var svc in g)
                group.Items.Add(svc);
            Groups.Add(group);
        }

        RebuildFilteredGroups();
    }

    partial void OnSearchTextChanged(string value) => RebuildFilteredGroups();

    private void RebuildFilteredGroups()
    {
        FilteredGroups.Clear();

        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? Services.AsEnumerable()
            : Services.Where(s =>
                s.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                s.GroupName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                s.TypeName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        var grouped = filtered
            .GroupBy(s => string.IsNullOrWhiteSpace(s.Config.GroupName) ? "General" : s.Config.GroupName)
            .OrderBy(g => g.Key)
            .ToList();

        var showHeaders = grouped.Count > 1 || (grouped.Count == 1 && grouped[0].Key != "General");

        foreach (var g in grouped)
        {
            var group = new ServiceGroupViewModel(g.Key) { ShowHeader = showHeaders };
            foreach (var svc in g)
                group.Items.Add(svc);
            FilteredGroups.Add(group);
        }
    }

    [RelayCommand]
    private void AddService()
    {
        var editorVm = new ServiceEditorViewModel();
        if (_dialogService.ShowServiceEditor(editorVm) != true) return;

        var config = editorVm.ToConfig();
        _serviceConfigs.Add(config);
        Services.Add(new ServiceItemViewModel(config, _windowsServiceController, _processService, _buildService, _logger, this));
        RebuildGroups();
        SaveConfiguration();
        StatusText = $"Service '{config.DisplayName}' added";
    }

    public void EditService(ServiceItemViewModel serviceVm)
    {
        var editorVm = new ServiceEditorViewModel(serviceVm.Config);
        if (_dialogService.ShowServiceEditor(editorVm) != true) return;

        editorVm.ApplyTo(serviceVm.Config);
        serviceVm.NotifyDisplayPropertiesChanged();
        RebuildGroups();
        SaveConfiguration();
        StatusText = $"Service '{serviceVm.Config.DisplayName}' updated";
    }

    public void DeleteService(ServiceItemViewModel serviceVm)
    {
        if (!_dialogService.Confirm(
            $"Remove '{serviceVm.DisplayName}' from AppPilot?\n\nThis will not stop or uninstall the service.",
            "Remove Service")) return;

        _serviceConfigs.Remove(serviceVm.Config);
        Services.Remove(serviceVm);
        RebuildGroups();
        SaveConfiguration();
        StatusText = $"Service '{serviceVm.DisplayName}' removed";
    }

    private void SaveConfiguration()
    {
        var settings = _configService.Load();
        settings.Services = _serviceConfigs;
        _configService.Save(settings);
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
        foreach (var group in Groups)
        {
            group.GroupAccentBrush = ColorProvider.GetGroupBrush(group.GroupName, !IsLightTheme);
            var groupColor = ColorProvider.GetGroupColor(group.GroupName, !IsLightTheme);
            group.GroupBadgeBrush = new SolidColorBrush(Color.FromArgb((byte)(!IsLightTheme ? 40 : 35), groupColor.R, groupColor.G, groupColor.B));
        }
        RebuildFilteredGroups();
    }

    public void Shutdown()
    {
        _pollingTimer.Stop();
    }
}
