using AppPilot.Domain.Enums;
using AppPilot.Models;
using Microsoft.Extensions.Logging;
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

/// <summary>
/// Controls Windows Services with on-demand UAC elevation.
/// Administrator permissions are requested only when needed for service operations.
/// </summary>
public class WindowsServiceController : IServiceController
{
    private readonly ILogger<WindowsServiceController> _logger;
    private readonly IElevationService _elevationService;

    private const int ServiceTimeoutSeconds = 30;

    public WindowsServiceController(
        ILogger<WindowsServiceController> logger,
        IElevationService elevationService)
    {
        _logger = logger;
        _elevationService = elevationService;
    }

    public async Task<bool> InstallAsync(ManagedServiceConfig config, CancellationToken cancellationToken = default)
    {
        try
        {
            // Use sc.exe with elevation to create the service
            var arguments = $"create \"{config.Name}\" binPath= \"{config.ExecutablePath} {config.Arguments}\" start= demand";

            var result = await _elevationService.RunElevatedAsync(
                "sc.exe",
                arguments,
                $"Install service '{config.DisplayName}'",
                cancellationToken);

            if (!result.Success)
            {
                _logger.LogError("Failed to install service {Name}: {Message}", config.Name, result.ErrorMessage);
                throw new InvalidOperationException(result.ErrorMessage);
            }

            // Set the description (also requires elevation)
            await SetDescriptionAsync(config, cancellationToken);

            _logger.LogInformation("Service {Name} installed successfully", config.Name);
            return true;
        }
        catch (OperationCanceledException) { throw; }
        catch (InvalidOperationException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while installing service {Name}", config.Name);
            throw new InvalidOperationException($"Failed to install '{config.DisplayName}': {ex.Message}", ex);
        }
    }

    private async Task SetDescriptionAsync(ManagedServiceConfig config, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _elevationService.RunElevatedAsync(
                "sc.exe",
                $"description \"{config.Name}\" \"{config.DisplayName}\"",
                $"Set description for service '{config.DisplayName}'",
                cancellationToken);

            if (!result.Success)
            {
                _logger.LogWarning("Failed to set service description for {Name}: {Message}",
                    config.Name, result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set service description for {Name}", config.Name);
        }
    }

    public async Task<bool> UninstallAsync(ManagedServiceConfig config, CancellationToken cancellationToken = default)
    {
        try
        {
            var status = GetStatus(config);
            if (status == ServiceStatus.Running || status == ServiceStatus.Starting)
                await StopAsync(config, cancellationToken);

            var result = await _elevationService.RunElevatedAsync(
                "sc.exe",
                $"delete \"{config.Name}\"",
                $"Uninstall service '{config.DisplayName}'",
                cancellationToken);

            if (!result.Success)
            {
                _logger.LogError("Failed to uninstall service {Name}: {Message}", config.Name, result.ErrorMessage);
                throw new InvalidOperationException(result.ErrorMessage);
            }

            _logger.LogInformation("Service {Name} uninstalled successfully", config.Name);
            return true;
        }
        catch (OperationCanceledException) { throw; }
        catch (InvalidOperationException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while uninstalling service {Name}", config.Name);
            throw new InvalidOperationException($"Failed to uninstall '{config.DisplayName}': {ex.Message}", ex);
        }
    }

    public async Task<bool> StartAsync(ManagedServiceConfig config, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check current status first (doesn't require elevation)
            var currentStatus = GetStatus(config);
            if (currentStatus == ServiceStatus.Running)
            {
                _logger.LogInformation("Service {Name} is already running", config.Name);
                return true;
            }

            // Use sc.exe start with elevation
            var result = await _elevationService.RunElevatedAsync(
                "sc.exe",
                $"start \"{config.Name}\"",
                $"Start service '{config.DisplayName}'",
                cancellationToken);

            if (!result.Success)
            {
                _logger.LogError("Failed to start service {Name}: {Message}", config.Name, result.ErrorMessage);
                throw new InvalidOperationException(result.ErrorMessage);
            }

            // Wait for the service to reach the running state
            var deadline = DateTime.UtcNow.AddSeconds(ServiceTimeoutSeconds);
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(500, cancellationToken);

                var status = GetStatus(config);
                if (status == ServiceStatus.Running)
                {
                    _logger.LogInformation("Service {Name} started successfully", config.Name);
                    return true;
                }

                if (status == ServiceStatus.Stopped || status == ServiceStatus.Error)
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
            _logger.LogError(ex, "Failed to start service {Name}", config.Name);
            throw new InvalidOperationException($"Failed to start '{config.DisplayName}': {ex.Message}", ex);
        }
    }

    public async Task<bool> StopAsync(ManagedServiceConfig config, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check current status first (doesn't require elevation)
            var currentStatus = GetStatus(config);
            if (currentStatus == ServiceStatus.Stopped || currentStatus == ServiceStatus.NotInstalled)
            {
                _logger.LogInformation("Service {Name} is already stopped", config.Name);
                return true;
            }

            // Use sc.exe stop with elevation
            var result = await _elevationService.RunElevatedAsync(
                "sc.exe",
                $"stop \"{config.Name}\"",
                $"Stop service '{config.DisplayName}'",
                cancellationToken);

            if (!result.Success)
            {
                _logger.LogError("Failed to stop service {Name}: {Message}", config.Name, result.ErrorMessage);
                throw new InvalidOperationException(result.ErrorMessage);
            }

            // Wait for the service to stop
            var deadline = DateTime.UtcNow.AddSeconds(ServiceTimeoutSeconds);
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(500, cancellationToken);

                var status = GetStatus(config);
                if (status == ServiceStatus.Stopped || status == ServiceStatus.NotInstalled)
                {
                    _logger.LogInformation("Service {Name} stopped successfully", config.Name);
                    return true;
                }
            }

            throw new System.TimeoutException(
                $"'{config.DisplayName}' did not stop within {ServiceTimeoutSeconds} seconds.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (System.TimeoutException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop service {Name}", config.Name);
            throw new InvalidOperationException($"Failed to stop '{config.DisplayName}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets the current status of a Windows service.
    /// This operation does not require elevation.
    /// </summary>
    public ServiceStatus GetStatus(ManagedServiceConfig config)
    {
        try
        {
            using var serviceController = new ServiceController(config.Name);

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
            _logger.LogWarning(ex, "Failed to get status for service {Name}", config.Name);
            return ServiceStatus.Error;
        }
    }
}
