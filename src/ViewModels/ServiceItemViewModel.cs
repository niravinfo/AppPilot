using System;
using System.Diagnostics;
using System.Threading.Tasks;
using AppPilot.Domain.Enums;
using AppPilot.Models;
using AppPilot.Services.Build;
using AppPilot.Services.ServiceControl;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace AppPilot.ViewModels;

public partial class ServiceItemViewModel : ViewModelBase
{
    private readonly IServiceController _windowsServiceController;
    private readonly IProcessService _processService;
    private readonly IBuildService _buildService;
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
    public string GroupName => Config.GroupName;
    public string TypeName => Config.Type.ToString();
    public string Port => Config.Port?.ToString() ?? "-";
    public string StatusText => Status.ToString();
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool CanInstall => Config.Type == ServiceType.Worker && Status == ServiceStatus.NotInstalled;
    public bool CanUninstall => Config.Type == ServiceType.Worker && Status != ServiceStatus.NotInstalled;
    public bool CanStart => Status == ServiceStatus.Stopped || Status == ServiceStatus.Error || Status == ServiceStatus.NotInstalled;
    public bool CanStop => Status == ServiceStatus.Running;
    public bool CanRestart => Status == ServiceStatus.Running;

    public bool HasBrowserUrl => !string.IsNullOrWhiteSpace(Config.HealthCheckUrl) || Config.Port.HasValue;
    public bool HasWorkingDirectory => !string.IsNullOrWhiteSpace(Config.WorkingDirectory);
    public bool HasCsprojPath => !string.IsNullOrWhiteSpace(Config.CsprojPath);

    public string BrowserUrl => !string.IsNullOrWhiteSpace(Config.HealthCheckUrl)
        ? Config.HealthCheckUrl
        : Config.Port.HasValue ? $"http://localhost:{Config.Port}" : string.Empty;

    public ServiceItemViewModel(
        ManagedServiceConfig config,
        IServiceController windowsServiceController,
        IProcessService processService,
        IBuildService buildService,
        ILogger logger,
        MainViewModel mainViewModel)
    {
        Config = config;
        _mainViewModel = mainViewModel;
        _windowsServiceController = windowsServiceController;
        _processService = processService;
        _buildService = buildService;
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

    [RelayCommand]
    private void OpenInBrowser()
    {
        if (string.IsNullOrEmpty(BrowserUrl)) return;
        try
        {
            Process.Start(new ProcessStartInfo(BrowserUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to open browser for {Name}", Config.Name);
        }
    }

    [RelayCommand]
    private void OpenDirectory()
    {
        if (!HasWorkingDirectory) return;
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{Config.WorkingDirectory}\"")
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to open directory for {Name}", Config.Name);
        }
    }

    [RelayCommand]
    private void OpenTerminal()
    {
        if (!HasWorkingDirectory) return;
        try
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "wt.exe",
                    Arguments = $"-d \"{Config.WorkingDirectory}\"",
                    UseShellExecute = true
                });
            }
            catch
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    WorkingDirectory = Config.WorkingDirectory,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to open terminal for {Name}", Config.Name);
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

    [RelayCommand]
    private void Edit() => _mainViewModel.EditService(this);

    [RelayCommand]
    private void Delete() => _mainViewModel.DeleteService(this);

    [RelayCommand]
    private async Task BuildAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = string.Empty;

        var wasRunning = Status == ServiceStatus.Running;
        try
        {
            if (wasRunning)
            {
                await StopAsync();
                await Task.Delay(300);
            }

            var exitCode = await _buildService.LaunchBuildAsync(Config.CsprojPath, Config.DisplayName);

            if (exitCode == 0 && wasRunning)
                await StartAsync();
            else if (exitCode != 0)
                ErrorMessage = "Build failed — check the terminal for details.";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Build failed for {Name}", Config.Name);
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void NotifyDisplayPropertiesChanged()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(GroupName));
        OnPropertyChanged(nameof(TypeName));
        OnPropertyChanged(nameof(Port));
        OnPropertyChanged(nameof(HasBrowserUrl));
        OnPropertyChanged(nameof(BrowserUrl));
        OnPropertyChanged(nameof(HasWorkingDirectory));
        OnPropertyChanged(nameof(HasCsprojPath));
        OnPropertyChanged(nameof(CanInstall));
        OnPropertyChanged(nameof(CanUninstall));
    }
}
