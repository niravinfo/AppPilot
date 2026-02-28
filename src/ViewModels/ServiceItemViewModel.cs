using AppPilot.Domain.Enums;
using AppPilot.Models;
using AppPilot.Services.ServiceControl;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System;
using System.Threading.Tasks;

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

    [ObservableProperty]
    private DateTime _lastChecked;

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

    public ServiceItemViewModel(ManagedServiceConfig config, MainViewModel mainViewModel)
    {
        Config = config;
        _mainViewModel = mainViewModel;
        _windowsServiceController = mainViewModel.GetType()
            .GetField("_windowsServiceController", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .GetValue(mainViewModel) as IServiceController 
            ?? throw new InvalidOperationException("WindowsServiceController not found");
        _processService = mainViewModel.GetType()
            .GetField("_processService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .GetValue(mainViewModel) as IProcessService
            ?? throw new InvalidOperationException("ProcessService not found");
        _logger = mainViewModel.GetType()
            .GetField("_logger", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .GetValue(mainViewModel) as ILogger
            ?? throw new InvalidOperationException("Logger not found");
    }

    [RelayCommand]
    public async Task StartAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            bool result;

            ErrorMessage = string.Empty;

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
                    result = _windowsServiceController.Install(Config);
                    if (!result)
                    {
                        ErrorMessage = "Failed to install service";
                        return;
                    }
                }

                result = _windowsServiceController.Start(Config);
                if (!result)
                {
                    ErrorMessage = "Failed to start service";
                }
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
        catch (Exception ex)
        {
            _logger.Error(ex, "Error starting service {Name}", Config.Name);
            ErrorMessage = ex.Message;
            Status = ServiceStatus.Error;
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanInstall));
            OnPropertyChanged(nameof(CanUninstall));
            OnPropertyChanged(nameof(CanStart));
            OnPropertyChanged(nameof(CanStop));
            OnPropertyChanged(nameof(CanRestart));
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
                _windowsServiceController.Stop(Config);
            }
            else
            {
                if (ProcessId.HasValue)
                {
                    _processService.Stop(Config, ProcessId.Value);
                    ProcessId = null;
                }
            }

            _mainViewModel.RefreshCommand.Execute(null);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error stopping service {Name}", Config.Name);
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanInstall));
            OnPropertyChanged(nameof(CanUninstall));
            OnPropertyChanged(nameof(CanStart));
            OnPropertyChanged(nameof(CanStop));
            OnPropertyChanged(nameof(CanRestart));
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
    public void Install()
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
            var result = _windowsServiceController.Install(Config);
            if (!result)
            {
                ErrorMessage = "Failed to install service";
            }

            _mainViewModel.RefreshCommand.Execute(null);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error installing service {Name}", Config.Name);
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanInstall));
            OnPropertyChanged(nameof(CanUninstall));
        }
    }

    [RelayCommand]
    public void Uninstall()
    {
        if (Config.Type != ServiceType.Worker || IsBusy) return;
        IsBusy = true;

        try
        {
            ErrorMessage = string.Empty;
            var result = _windowsServiceController.Uninstall(Config);
            if (!result)
            {
                ErrorMessage = "Failed to uninstall service";
            }

            _mainViewModel.RefreshCommand.Execute(null);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error uninstalling service {Name}", Config.Name);
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(CanInstall));
            OnPropertyChanged(nameof(CanUninstall));
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
