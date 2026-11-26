using System;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using AppPilot.AdminBroker;
using AppPilot.Domain.Enums;
using AppPilot.Models;
using Microsoft.Extensions.Logging;

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
/// Controls Windows Services through a single elevated broker process. Status reads
/// are performed locally because they do not require administrator privileges.
/// </summary>
public class WindowsServiceController : IServiceController
{
    private readonly ILogger<WindowsServiceController> _logger;
    private readonly IAdminBrokerClient _broker;

    public WindowsServiceController(
        ILogger<WindowsServiceController> logger,
        IAdminBrokerClient broker)
    {
        _logger = logger;
        _broker = broker;
    }

    public async Task<bool> InstallAsync(ManagedServiceConfig config, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(BrokerOperation.Install, config, cancellationToken);
        _logger.LogInformation("Service {Name} installed successfully", config.Name);
        return true;
    }

    public async Task<bool> UninstallAsync(ManagedServiceConfig config, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(BrokerOperation.Uninstall, config, cancellationToken);
        _logger.LogInformation("Service {Name} uninstalled successfully", config.Name);
        return true;
    }

    public async Task<bool> StartAsync(ManagedServiceConfig config, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(BrokerOperation.Start, config, cancellationToken);
        _logger.LogInformation("Service {Name} started successfully", config.Name);
        return true;
    }

    public async Task<bool> StopAsync(ManagedServiceConfig config, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(BrokerOperation.Stop, config, cancellationToken);
        _logger.LogInformation("Service {Name} stopped successfully", config.Name);
        return true;
    }

    private async Task ExecuteAsync(
        BrokerOperation operation,
        ManagedServiceConfig config,
        CancellationToken cancellationToken)
    {
        var result = await _broker.ExecuteAsync(operation, config, cancellationToken);
        if (result.Success)
            return;

        var message = result.WasCancelled
            ? "Administrator permission was declined. No changes were made."
            : result.Error;

        _logger.LogError("Broker operation {Operation} failed for {Name}: {Message}",
            operation, config.Name, message);
        throw new InvalidOperationException(message);
    }

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
