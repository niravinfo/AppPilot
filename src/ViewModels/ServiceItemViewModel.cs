using System;
using System.Threading.Tasks;
using AppPilot.Domain.Enums;
using AppPilot.Models;
using AppPilot.Services.ServiceControl;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace AppPilot.ViewModels;

public partial class ServiceItemViewModel : ViewModelBase
{
    private readonly IServiceController _windowsServiceController;
    private readonly IProcessService _processService;
    private readonly ILogger _logger;
    private readonly MainViewModel _mainViewModel;

    public ManagedServiceConfig Config { get; }

    [ObservableProperty]
    private ServiceStatus _status = ServiceStatus.NotInstalled;

    [ObservableProperty]
    private int? _processId;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public DateTime LastChecked { get; set; }

    [ObservableProperty]
    private bool _isBusy;

    public string DisplayName => Config.DisplayName;
    public string TypeName => Config.Type.ToString();
    public string Port => Config.Port?.ToString() ?? "-";
    public string StatusText => Status.ToString();
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool CanInstall => Config.Type == ServiceType.Worker && Status == ServiceStatus.NotInstalled;
    public bool CanUninstall => Config.Type == ServiceType.Worker && Status != ServiceStatus.NotInstalled;
    public bool CanStart => Status == ServiceStatus.Stopped || Status == ServiceStatus.Error || Status == ServiceStatus.NotInstalled;
    public bool CanStop => Status == ServiceStatus.Running;
    public bool CanRestart => Status == ServiceStatus.Running;

    public ServiceItemViewModel(
        ManagedServiceConfig config,
        IServiceController windowsServiceController,
        IProcessService processService,
        ILogger logger,
        MainViewModel mainViewModel)
    {
        Config = config;
        _mainViewModel = mainViewModel;
        _windowsServiceController = windowsServiceController;
        _processService = processService;
        _logger = logger;
    }

    [RelayCommand]
    public async Task StartAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            ErrorMessage = string.Empty;

            if (Config.Port.HasValue)
            {
                var portError = _processService.GetPortOwner(Config.Port.Value);
                if (portError != null)
                {
                    ErrorMessage = portError;
                    Status = ServiceStatus.Error;
                    return;
                }
            }

            if (Config.Type == ServiceType.Worker)
            {
                if (Status == ServiceStatus.NotInstalled)
                {
                    if (!System.IO.File.Exists(Config.ExecutablePath))
                    {
                        ErrorMessage = $"Executable not found: {Config.ExecutablePath}";
                        Status = ServiceStatus.Error;
                        return;
                    }

                    await _windowsServiceController.InstallAsync(Config);
                }

                await _windowsServiceController.StartAsync(Config);
            }
            else
            {
                if (!System.IO.File.Exists(Config.ExecutablePath))
                {
                    ErrorMessage = $"Executable not found: {Config.ExecutablePath}";
                    Status = ServiceStatus.Error;
                    return;
                }
                var process = _processService.Start(Config);
                if (process == null)
                {
                    ErrorMessage = "Failed to start process";
                    Status = ServiceStatus.Error;
                }
                else
                {
                    ProcessId = process.Id;
                }
            }

            _mainViewModel.RefreshCommand.Execute(null);
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "Operation timed out or was cancelled.";
            Status = ServiceStatus.Error;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error starting service {Name}", Config.Name);
            ErrorMessage = ex.Message;
            Status = ServiceStatus.Error;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task StopAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            ErrorMessage = string.Empty;
            if (Config.Type == ServiceType.Worker)
            {
                await _windowsServiceController.StopAsync(Config);
            }
            else
            {
                var pidToStop = ProcessId ?? _processService.GetProcessId(Config);
                if (pidToStop.HasValue)
                {
                    await _processService.StopAsync(Config, pidToStop.Value);
                    ProcessId = null;
                }
                else
                {
                    ErrorMessage = $"Cannot find the running process for '{Config.DisplayName}'.";
                }
            }

            _mainViewModel.RefreshCommand.Execute(null);
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "Operation timed out or was cancelled.";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error stopping service {Name}", Config.Name);
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task RestartAsync()
    {
        if (Status == ServiceStatus.Running)
        {
            await StopAsync();
            await Task.Delay(1000);
        }
        await StartAsync();
    }

    [RelayCommand]
    public async Task InstallAsync()
    {
        if (Config.Type != ServiceType.Worker || IsBusy) return;
        IsBusy = true;

        try
        {
            ErrorMessage = string.Empty;
            if (!System.IO.File.Exists(Config.ExecutablePath))
            {
                ErrorMessage = $"Executable not found: {Config.ExecutablePath}";
                Status = ServiceStatus.Error;
                return;
            }

            await _windowsServiceController.InstallAsync(Config);
            _mainViewModel.RefreshCommand.Execute(null);
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "Operation timed out or was cancelled.";
            Status = ServiceStatus.Error;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error installing service {Name}", Config.Name);
            ErrorMessage = ex.Message;
            Status = ServiceStatus.Error;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task UninstallAsync()
    {
        if (Config.Type != ServiceType.Worker || IsBusy) return;
        IsBusy = true;

        try
        {
            ErrorMessage = string.Empty;
            await _windowsServiceController.UninstallAsync(Config);
            _mainViewModel.RefreshCommand.Execute(null);
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "Operation timed out or was cancelled.";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error uninstalling service {Name}", Config.Name);
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnStatusChanged(ServiceStatus value)
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(CanInstall));
        OnPropertyChanged(nameof(CanUninstall));
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanStop));
        OnPropertyChanged(nameof(CanRestart));
    }

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
    }
}
