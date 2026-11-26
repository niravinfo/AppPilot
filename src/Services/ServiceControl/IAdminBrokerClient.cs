using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AppPilot.AdminBroker;
using AppPilot.Models;

namespace AppPilot.Services.ServiceControl;

public sealed record AdminBrokerResult(bool Success, string Error, bool WasCancelled);

public interface IAdminBrokerClient : IDisposable
{
    Task<AdminBrokerResult> ExecuteAsync(
        BrokerOperation operation,
        ManagedServiceConfig config,
        CancellationToken cancellationToken = default);
}

public interface IBrokerPipeClient : IDisposable
{
    Task<BrokerResponse> SendAsync(BrokerRequest request, CancellationToken cancellationToken);
}

public interface IBrokerPipeClientFactory
{
    IBrokerPipeClient Create(string pipeName);
}

public interface IBrokerProcessLauncher
{
    Process? Launch(string exePath, string pipeName, int clientPid, string clientSid);
}
