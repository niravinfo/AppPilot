using AppPilot.Domain.Enums;
using AppPilot.Models;
using Serilog;
using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;

namespace AppPilot.Services.ServiceControl;

public interface IServiceController
{
    Task<bool> InstallAsync(ManagedServiceConfig config, CancellationToken cancellationToken = default);
    Task<bool> UninstallAsync(ManagedServiceConfig config, CancellationToken cancellationToken = default);
    Task<bool> StartAsync(ManagedServiceConfig config, CancellationToken cancellationToken = default);
    Task<bool> StopAsync(ManagedServiceConfig config, CancellationToken cancellationToken = default);
    ServiceStatus GetStatus(ManagedServiceConfig config);
}

public class WindowsServiceController : IServiceController
{
    private readonly ILogger _logger;

    private const int ServiceTimeoutSeconds = 30;

    public WindowsServiceController(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<bool> InstallAsync(ManagedServiceConfig config, CancellationToken cancellationToken = default)
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

            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to launch sc.exe.");
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var output = await outputTask;

            if (process.ExitCode != 0)
            {
                var msg = string.IsNullOrWhiteSpace(output)
                    ? $"sc.exe exited with code {process.ExitCode}."
                    : output.Trim();
                _logger.Error("Failed to install service {Name}: {Message}", config.Name, msg);
                throw new InvalidOperationException(msg);
            }

            await SetDescriptionAsync(config, cancellationToken);
            _logger.Information("Service {Name} installed successfully", config.Name);
            return true;
        }
        catch (OperationCanceledException) { throw; }
        catch (InvalidOperationException) { throw; }
        catch (Exception ex)
        {
            _logger.Error(ex, "Exception while installing service {Name}", config.Name);
            throw new InvalidOperationException($"Failed to install '{config.DisplayName}': {ex.Message}", ex);
        }
    }

    private async Task SetDescriptionAsync(ManagedServiceConfig config, CancellationToken cancellationToken)
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
            using var process = Process.Start(startInfo);
            if (process != null)
                await process.WaitForExitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to set service description for {Name}", config.Name);
        }
    }

    public async Task<bool> UninstallAsync(ManagedServiceConfig config, CancellationToken cancellationToken = default)
    {
        try
        {
            var status = GetStatus(config);
            if (status == ServiceStatus.Running || status == ServiceStatus.Starting)
                await StopAsync(config, cancellationToken);

            var startInfo = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"delete {config.Name}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to launch sc.exe.");
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var output = await outputTask;

            if (process.ExitCode != 0)
            {
                var msg = string.IsNullOrWhiteSpace(output)
                    ? $"sc.exe exited with code {process.ExitCode}."
                    : output.Trim();
                _logger.Error("Failed to uninstall service {Name}: {Message}", config.Name, msg);
                throw new InvalidOperationException(msg);
            }

            _logger.Information("Service {Name} uninstalled successfully", config.Name);
            return true;
        }
        catch (OperationCanceledException) { throw; }
        catch (InvalidOperationException) { throw; }
        catch (Exception ex)
        {
            _logger.Error(ex, "Exception while uninstalling service {Name}", config.Name);
            throw new InvalidOperationException($"Failed to uninstall '{config.DisplayName}': {ex.Message}", ex);
        }
    }

    public async Task<bool> StartAsync(ManagedServiceConfig config, CancellationToken cancellationToken = default)
    {
        try
        {
            using var sc = new ServiceController(config.Name);
            sc.Refresh();

            if (sc.Status == ServiceControllerStatus.Running)
            {
                _logger.Information("Service {Name} is already running", config.Name);
                return true;
            }

            sc.Start();

            var deadline = DateTime.UtcNow.AddSeconds(ServiceTimeoutSeconds);
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(500, cancellationToken);
                sc.Refresh();

                if (sc.Status == ServiceControllerStatus.Running)
                {
                    _logger.Information("Service {Name} started successfully", config.Name);
                    return true;
                }

                if (sc.Status == ServiceControllerStatus.Stopped)
                {
                    throw new InvalidOperationException(
                        $"'{config.DisplayName}' stopped immediately after launching. " +
                        "Ensure the project calls UseWindowsService() and references the " +
                        "Microsoft.Extensions.Hosting.WindowsServices NuGet package.");
                }
            }

            throw new System.TimeoutException(
                $"'{config.DisplayName}' did not reach Running state within {ServiceTimeoutSeconds} seconds.");
        }
        catch (OperationCanceledException) { throw; }
        catch (InvalidOperationException) { throw; }
        catch (System.TimeoutException) { throw; }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to start service {Name}", config.Name);
            throw new InvalidOperationException($"Failed to start '{config.DisplayName}': {ex.Message}", ex);
        }
    }

    public async Task<bool> StopAsync(ManagedServiceConfig config, CancellationToken cancellationToken = default)
    {
        try
        {
            using var sc = new ServiceController(config.Name);
            sc.Refresh();

            if (sc.Status == ServiceControllerStatus.Stopped)
            {
                _logger.Information("Service {Name} is already stopped", config.Name);
                return true;
            }

            sc.Stop();

            var deadline = DateTime.UtcNow.AddSeconds(ServiceTimeoutSeconds);
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(500, cancellationToken);
                sc.Refresh();

                if (sc.Status == ServiceControllerStatus.Stopped)
                {
                    _logger.Information("Service {Name} stopped successfully", config.Name);
                    return true;
                }
            }

            throw new System.TimeoutException(
                $"'{config.DisplayName}' did not stop within {ServiceTimeoutSeconds} seconds.");
        }
        catch (OperationCanceledException) { throw; }
        catch (System.TimeoutException) { throw; }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to stop service {Name}", config.Name);
            throw new InvalidOperationException($"Failed to stop '{config.DisplayName}': {ex.Message}", ex);
        }
    }

    public ServiceStatus GetStatus(ManagedServiceConfig config)
    {
        try
        {
            using var serviceController = new System.ServiceProcess.ServiceController(config.Name);

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
