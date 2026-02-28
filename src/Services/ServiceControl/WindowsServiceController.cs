using AppPilot.Domain.Enums;
using AppPilot.Models;
using Serilog;
using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading;

namespace AppPilot.Services.ServiceControl;

public interface IServiceController
{
    bool Install(ManagedServiceConfig config);
    bool Uninstall(ManagedServiceConfig config);
    bool Start(ManagedServiceConfig config);
    bool Stop(ManagedServiceConfig config);
    ServiceStatus GetStatus(ManagedServiceConfig config);
}

public class WindowsServiceController : IServiceController
{
    private readonly ILogger _logger;

    public WindowsServiceController(ILogger logger)
    {
        _logger = logger;
    }

    public bool Install(ManagedServiceConfig config)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"create {config.Name} binPath= \"{config.ExecutablePath} {config.Arguments}\" start= demand",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            process?.WaitForExit();

            if (process?.ExitCode == 0)
            {
                SetDescription(config);
                _logger.Information("Service {Name} installed successfully", config.Name);
                return true;
            }

            _logger.Error("Failed to install service {Name}, exit code: {ExitCode}", config.Name, process?.ExitCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Exception while installing service {Name}", config.Name);
            return false;
        }
    }

    private void SetDescription(ManagedServiceConfig config)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"description {config.Name} \"{config.DisplayName}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to set service description");
        }
    }

    public bool Uninstall(ManagedServiceConfig config)
    {
        try
        {
            Stop(config);

            var startInfo = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"delete {config.Name}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            process?.WaitForExit();

            if (process?.ExitCode == 0)
            {
                _logger.Information("Service {Name} uninstalled successfully", config.Name);
                return true;
            }

            _logger.Error("Failed to uninstall service {Name}, exit code: {ExitCode}", config.Name, process?.ExitCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Exception while uninstalling service {Name}", config.Name);
            return false;
        }
    }

    public bool Start(ManagedServiceConfig config)
    {
        try
        {
            var serviceController = new System.ServiceProcess.ServiceController(config.Name);
            
            if (serviceController.Status == ServiceControllerStatus.Running)
            {
                _logger.Information("Service {Name} is already running", config.Name);
                return true;
            }

            serviceController.Start();
            serviceController.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
            _logger.Information("Service {Name} started successfully", config.Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to start service {Name}", config.Name);
            return false;
        }
    }

    public bool Stop(ManagedServiceConfig config)
    {
        try
        {
            var serviceController = new System.ServiceProcess.ServiceController(config.Name);
            
            if (serviceController.Status == ServiceControllerStatus.Stopped)
            {
                _logger.Information("Service {Name} is already stopped", config.Name);
                return true;
            }

            serviceController.Stop();
            serviceController.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
            _logger.Information("Service {Name} stopped successfully", config.Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to stop service {Name}", config.Name);
            return false;
        }
    }

    public ServiceStatus GetStatus(ManagedServiceConfig config)
    {
        try
        {
            var serviceController = new System.ServiceProcess.ServiceController(config.Name);

            return serviceController.Status switch
            {
                ServiceControllerStatus.Stopped => ServiceStatus.Stopped,
                ServiceControllerStatus.StartPending => ServiceStatus.Starting,
                ServiceControllerStatus.Running => ServiceStatus.Running,
                ServiceControllerStatus.StopPending => ServiceStatus.Stopping,
                _ => ServiceStatus.Error
            };
        }
        catch (InvalidOperationException)
        {
            return ServiceStatus.NotInstalled;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to get status for service {Name}", config.Name);
            return ServiceStatus.Error;
        }
    }
}
