using AppPilot.Domain.Enums;
using AppPilot.Models;
using AppPilot.Services.Configuration;
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
    private readonly DispatcherTimer _pollingTimer;

    [ObservableProperty]
    private ObservableCollection<ServiceItemViewModel> _services = new();

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _lastUpdateTime = "-";

    [ObservableProperty]
    private bool _isLoading;

    private List<ManagedServiceConfig> _serviceConfigs = new();

    public MainViewModel(
        IConfigurationService configService,
        IServiceController windowsServiceController,
        IProcessService processService,
        IHealthChecker healthChecker,
        ILogger logger)
    {
        _configService = configService;
        _windowsServiceController = windowsServiceController;
        _processService = processService;
        _healthChecker = healthChecker;
        _logger = logger;

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
        {
            _pollingTimer.Interval = TimeSpan.FromMilliseconds(settings.AppPilot.PollingIntervalMs);
        }

        Services.Clear();
        foreach (var config in _serviceConfigs.OrderBy(s => s.StartOrder))
        {
            Services.Add(new ServiceItemViewModel(config, _windowsServiceController, _processService, _logger, this));
        }

        StatusText = $"Loaded {_serviceConfigs.Count} services";
    }

    [RelayCommand]
    private void Refresh()
    {
        _ = RefreshStatusAsync();
    }

    private async Task RefreshStatusAsync()
    {
        foreach (var service in Services)
        {
            await UpdateServiceStatusAsync(service);
        }

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
                var isInstalled = _windowsServiceController.GetStatus(config) != ServiceStatus.NotInstalled;

                if (!isInstalled)
                {
                    status = ServiceStatus.NotInstalled;
                }
                else
                {
                    status = _windowsServiceController.GetStatus(config);
                }
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

    public void Shutdown()
    {
        _pollingTimer.Stop();
    }
}
