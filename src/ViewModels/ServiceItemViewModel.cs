using AppPilot.Domain.Enums;
using AppPilot.Models;
using AppPilot.Services;
using AppPilot.Services.Build;
using AppPilot.Services.ServiceControl;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Media;

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
    public string GroupId => Config.GroupId;

    public string GroupName { get; }
    public string TypeName => Config.Type.ToString();
    public string Port => Config.Port?.ToString() ?? "-";
    public string StatusText => Status.ToString();
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    [ObservableProperty]
    private Brush _typeBadgeBrush = Brushes.Gray;
    [ObservableProperty]
    private Brush _typeBadgeFgBrush = Brushes.Gray;
    [ObservableProperty]
    private Brush _groupAccentBrush = Brushes.Gray;
    [ObservableProperty]
    private Brush _groupBadgeBrush = Brushes.Gray;

    public bool CanInstall => Config.Type == ServiceType.Worker && Status == ServiceStatus.NotInstalled;
    public bool CanUninstall => Config.Type == ServiceType.Worker && Status != ServiceStatus.NotInstalled;
    public bool UseWindowsService => Config.Type == ServiceType.Worker && Config.UseWindowsService;
    public bool CanStart => Status == ServiceStatus.Stopped || Status == ServiceStatus.Error || Status == ServiceStatus.NotInstalled;
    public bool CanStop => Status == ServiceStatus.Running;
    public bool CanRestart => Status == ServiceStatus.Running;

    public bool HasBrowserUrl => Config.Type != ServiceType.Worker && (!string.IsNullOrWhiteSpace(Config.HealthCheckUrl) || Config.Port.HasValue);
    public bool HasWorkingDirectory => !string.IsNullOrWhiteSpace(Config.WorkingDirectory);
    public bool HasCsprojPath => !string.IsNullOrWhiteSpace(Config.CsprojPath);

    public string BrowserUrl => !string.IsNullOrWhiteSpace(Config.HealthCheckUrl)
        ? Config.HealthCheckUrl
        : Config.Port.HasValue ? $"http://localhost:{Config.Port}" : string.Empty;

    public ServiceItemViewModel(
        ManagedServiceConfig config,
        string groupName,
        IServiceController windowsServiceController,
        IProcessService processService,
        IBuildService buildService,
        ILogger logger,
        MainViewModel mainViewModel)
    {
        Config = config;
        GroupName = groupName;
        _windowsServiceController = windowsServiceController;
        _processService = processService;
        _buildService = buildService;
        _logger = logger;
        _mainViewModel = mainViewModel;
        InitializeColors();
    }


    public Brush StatusTypeBarBrush
    {
        get
        {
            // gRPC: #818cf8 (running), #6366f1 (all other states)
            // API: #22d3ee (running), #0ea5e9 (all other states)
            // Worker: #f59e0b (running), #f97316 (all other states)
            bool isRunning = Status == ServiceStatus.Running;
            switch (Config.Type)
            {
                case ServiceType.Grpc:
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString(isRunning ? "#FF818cf8" : "#FF6366f1"));
                case ServiceType.WebApi:
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString(isRunning ? "#FF22d3ee" : "#FF0ea5e9"));
                case ServiceType.Worker:
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString(isRunning ? "#FFf59e0b" : "#FFf97316"));
                default:
                    return Brushes.Gray;
            }
        }
    }

    private void InitializeColors()
    {
        var isDark = !ThemeManager.IsLight;

        var typeColor = ColorProvider.GetServiceTypeColor(Config.Type, isDark);
        TypeBadgeBrush = new SolidColorBrush(Color.FromArgb((byte)(isDark ? 50 : 40), typeColor.R, typeColor.G, typeColor.B));
        TypeBadgeFgBrush = new SolidColorBrush(typeColor);

        GroupConfig group = null;
        if (_mainViewModel != null && !string.IsNullOrWhiteSpace(Config.GroupId) && _mainViewModel._groupDict != null)
            _mainViewModel._groupDict.TryGetValue(Config.GroupId, out group);

        Color groupColor;
        if (!string.IsNullOrWhiteSpace(group?.ColorCode))
        {
            groupColor = (Color)ColorConverter.ConvertFromString(group.ColorCode);
        }
        else
        {
            groupColor = ColorProvider.GetGroupColor(group?.Name ?? Config.GroupId ?? "", isDark);
        }

        GroupAccentBrush = new SolidColorBrush(groupColor);
        GroupBadgeBrush = new SolidColorBrush(Color.FromArgb((byte)(isDark ? 30 : 25), groupColor.R, groupColor.G, groupColor.B));
    }

    public void RefreshColors()
    {
        InitializeColors();
        OnPropertyChanged(nameof(TypeBadgeBrush));
        OnPropertyChanged(nameof(TypeBadgeFgBrush));
        OnPropertyChanged(nameof(GroupAccentBrush));
        OnPropertyChanged(nameof(GroupBadgeBrush));
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

            if (Config.Type == ServiceType.Worker && Config.UseWindowsService)
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
                    OnPropertyChanged(nameof(StatusTypeBarBrush));
                }
                _mainViewModel.RefreshCommand.Execute(null);
            }
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "Operation timed out or was cancelled.";
            Status = ServiceStatus.Error;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting service {Name}", Config.Name);
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
            if (Config.Type == ServiceType.Worker && Config.UseWindowsService)
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
            _logger.LogError(ex, "Error stopping service {Name}", Config.Name);
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
        // Ensure CanStop/CanRestart are correct for worker services in both modes
        // Status logic is handled in MainViewModel, which is now correct for both modes
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
            _logger.LogError(ex, "Error installing service {Name}", Config.Name);
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
            _logger.LogError(ex, "Error uninstalling service {Name}", Config.Name);
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
            _logger.LogError(ex, "Failed to open browser for {Name}", Config.Name);
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
            _logger.LogError(ex, "Failed to open directory for {Name}", Config.Name);
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
            _logger.LogError(ex, "Failed to open terminal for {Name}", Config.Name);
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
        OnPropertyChanged(nameof(StatusTypeBarBrush));
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
            _logger.LogError(ex, "Build failed for {Name}", Config.Name);
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
