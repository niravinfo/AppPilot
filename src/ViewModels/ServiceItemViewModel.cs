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
    private readonly GroupInfo _groupInfo;
    private readonly Action<ServiceItemViewModel>? _editCallback;
    private readonly Action<ServiceItemViewModel>? _deleteCallback;
    private readonly Action<ServiceItemViewModel>? _onStatusChangedCallback;

    public ManagedServiceConfig Config { get; }

    [ObservableProperty]
    private ServiceStatus _status = ServiceStatus.NotInstalled;

    [ObservableProperty]
    private int? _processId;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public DateTime LastChecked { get; set; }

    private DateTime _nextRefreshTime = DateTime.MinValue;
    private DateTime _acceleratedRefreshUntil = DateTime.MinValue;
    private int _acceleratedRefreshAttempts;
    private static readonly TimeSpan[] AcceleratedIntervals =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(3),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10)
    ];
    private const int MaxAcceleratedAttempts = 5;

    public DateTime NextRefreshTime
    {
        get => _nextRefreshTime;
        set => _nextRefreshTime = value;
    }

    public bool NeedsRefresh
    {
        get
        {
            if (DateTime.Now < _acceleratedRefreshUntil)
                return true;
            return DateTime.Now >= _nextRefreshTime;
        }
    }

    public void ScheduleNextRefresh(TimeSpan interval)
    {
        _nextRefreshTime = DateTime.Now.Add(interval);
    }

    public void MarkAsNeedingRefresh()
    {
        _nextRefreshTime = DateTime.MinValue;
    }

    public void StartAcceleratedRefresh()
    {
        _acceleratedRefreshAttempts = 0;
        _acceleratedRefreshUntil = DateTime.Now.Add(AcceleratedIntervals[0]);
        _nextRefreshTime = DateTime.MinValue;
    }

    public bool TryScheduleNextAcceleratedRefresh()
    {
        _acceleratedRefreshAttempts++;
        if (_acceleratedRefreshAttempts >= MaxAcceleratedAttempts)
        {
            _acceleratedRefreshUntil = DateTime.MinValue;
            return false;
        }

        _acceleratedRefreshUntil = DateTime.Now.Add(AcceleratedIntervals[_acceleratedRefreshAttempts]);
        return true;
    }

    public void CancelAcceleratedRefresh()
    {
        _acceleratedRefreshUntil = DateTime.MinValue;
        _acceleratedRefreshAttempts = 0;
    }

    [ObservableProperty]
    private bool _isBusy;

    public string DisplayName => Config.DisplayName;
    public string GroupId => Config.GroupId;

    public string GroupName => _groupInfo.Name;

    // Optimize: Cache type name string to avoid repeated ToString() calls
    private string? _cachedTypeName;
    public string TypeName => _cachedTypeName ??= Config.Type.ToString();

    // Optimize: Cache port string to avoid repeated allocation
    private string? _cachedPort;
    public string Port => _cachedPort ??= Config.Port?.ToString() ?? "-";

    // Optimize: Cache browser URL to avoid repeated string building
    public string BrowserUrl
    {
        get
        {
            if (field == null)
            {
                if (!string.IsNullOrWhiteSpace(Config.HealthCheckUrl))
                {
                    field = Config.HealthCheckUrl;
                }
                else if (Config.Port.HasValue)
                {
                    field = $"http://localhost:{Config.Port}";
                }
                else
                {
                    field = string.Empty;
                }
            }

            return field;
        }
    }

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

    // Optimize: Cache status bar brushes to avoid repeated allocations
    private SolidColorBrush? _runningBrush;
    private SolidColorBrush? _stoppedBrush;
    private bool _lastThemeWasLight = true;

    public Brush StatusTypeBarBrush
    {
        get
        {
            bool isRunning = Status == ServiceStatus.Running;
            bool isDark = !ThemeManager.IsLight;

            if (_lastThemeWasLight != ThemeManager.IsLight)
            {
                _runningBrush = null;
                _stoppedBrush = null;
                _lastThemeWasLight = ThemeManager.IsLight;
            }

            if (isRunning)
            {
                if (_runningBrush == null)
                {
                    var color = Config.Type switch
                    {
                        ServiceType.Grpc => ColorProvider.GetServiceTypeColor(ServiceType.Grpc, isDark),
                        ServiceType.WebApi => ColorProvider.GetServiceTypeColor(ServiceType.WebApi, isDark),
                        ServiceType.Worker => ColorProvider.GetServiceTypeColor(ServiceType.Worker, isDark),
                        _ => Color.FromRgb(128, 128, 128)
                    };
                    _runningBrush = new SolidColorBrush(color);
                    _runningBrush.Freeze();
                }

                return _runningBrush;
            }
            else
            {
                if (_stoppedBrush == null)
                {
                    var baseColor = Config.Type switch
                    {
                        ServiceType.Grpc => ColorProvider.GetServiceTypeColor(ServiceType.Grpc, isDark),
                        ServiceType.WebApi => ColorProvider.GetServiceTypeColor(ServiceType.WebApi, isDark),
                        ServiceType.Worker => ColorProvider.GetServiceTypeColor(ServiceType.Worker, isDark),
                        _ => Color.FromRgb(96, 96, 96)
                    };

                    var alpha = (byte)(isDark ? 0x99 : 0xB3);
                    var color = Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B);
                    _stoppedBrush = new SolidColorBrush(color);
                    _stoppedBrush.Freeze();
                }

                return _stoppedBrush;
            }
        }
    }

    public ServiceItemViewModel(
        ManagedServiceConfig config,
        GroupInfo groupInfo,
        IServiceController windowsServiceController,
        IProcessService processService,
        IBuildService buildService,
        ILogger logger,
        Action<ServiceItemViewModel>? editCallback = null,
        Action<ServiceItemViewModel>? deleteCallback = null,
        Action<ServiceItemViewModel>? onStatusChangedCallback = null)
    {
        Config = config;
        _groupInfo = groupInfo;
        _windowsServiceController = windowsServiceController;
        _processService = processService;
        _buildService = buildService;
        _logger = logger;
        _editCallback = editCallback;
        _deleteCallback = deleteCallback;
        _onStatusChangedCallback = onStatusChangedCallback;
        InitializeColors();
    }

    private void InitializeColors()
    {
        // Use ThemeManager's cached brushes to avoid repeated allocations (~2 MB savings)
        TypeBadgeBrush = ThemeManager.GetServiceTypeBadgeBrush(Config.Type);
        TypeBadgeFgBrush = ThemeManager.GetServiceTypeBrush(Config.Type);

        GroupAccentBrush = ThemeManager.GetGroupBrush(_groupInfo.Id, _groupInfo.Name, _groupInfo.ColorCode);
        GroupBadgeBrush = ThemeManager.GetGroupBadgeBrush(_groupInfo.Id, _groupInfo.Name, _groupInfo.ColorCode);
    }

    public void RefreshColors()
    {
        _runningBrush = null;
        _stoppedBrush = null;
        InitializeColors();
        OnPropertyChanged(nameof(TypeBadgeBrush));
        OnPropertyChanged(nameof(TypeBadgeFgBrush));
        OnPropertyChanged(nameof(GroupAccentBrush));
        OnPropertyChanged(nameof(GroupBadgeBrush));
        OnPropertyChanged(nameof(StatusTypeBarBrush));
    }

    [RelayCommand]
    public async Task StartAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            ErrorMessage = string.Empty;
            var originalStatus = Status;
            Status = ServiceStatus.Starting;

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
                if (originalStatus == ServiceStatus.NotInstalled)
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
                Status = ServiceStatus.Running;
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
                    Status = ServiceStatus.Running;
                    OnPropertyChanged(nameof(StatusTypeBarBrush));
                }
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
            Status = ServiceStatus.Stopping;

            if (Config.Type == ServiceType.Worker && Config.UseWindowsService)
            {
                await _windowsServiceController.StopAsync(Config);
                Status = ServiceStatus.Stopped;
            }
            else
            {
                var pidToStop = ProcessId ?? _processService.GetProcessId(Config);

                if (pidToStop.HasValue)
                {
                    await _processService.StopAsync(Config, pidToStop.Value);
                    ProcessId = null;
                    Status = ServiceStatus.Stopped;
                }
                else
                {
                    ErrorMessage = $"Cannot find the running process for '{Config.DisplayName}'.";
                }
            }
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
        _onStatusChangedCallback?.Invoke(this);
    }

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    [RelayCommand]
    private void Edit() => _editCallback?.Invoke(this);

    [RelayCommand]
    private void Delete() => _deleteCallback?.Invoke(this);

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
