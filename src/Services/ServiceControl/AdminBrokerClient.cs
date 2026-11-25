using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using AppPilot.AdminBroker;
using AppPilot.Models;

namespace AppPilot.Services.ServiceControl;

public sealed class AdminBrokerClient : IAdminBrokerClient
{
    private readonly IBrokerProcessLauncher _launcher;
    private readonly IBrokerPipeClientFactory _pipeFactory;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private Process? _broker;
    private IBrokerPipeClient? _pipe;

    public AdminBrokerClient()
        : this(new DefaultBrokerProcessLauncher(), new DefaultBrokerPipeClientFactory())
    {
    }

    public AdminBrokerClient(IBrokerProcessLauncher launcher, IBrokerPipeClientFactory pipeFactory)
    {
        _launcher = launcher;
        _pipeFactory = pipeFactory;
    }

    public async Task<AdminBrokerResult> ExecuteAsync(
        BrokerOperation operation,
        ManagedServiceConfig config,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_pipe == null)
            {
                var startResult = StartBroker();
                if (!startResult.Success)
                    return startResult;
            }

            var request = new BrokerRequest
            {
                Operation = operation,
                Name = config.Name,
                DisplayName = config.DisplayName,
                ExecutablePath = config.ExecutablePath,
                Arguments = config.Arguments
            };

            try
            {
                var response = await _pipe!.SendAsync(request, cancellationToken);
                return new AdminBrokerResult(response.Success, response.Error, false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException)
            {
                Reset();
                return new AdminBrokerResult(
                    false,
                    "Connection to the elevated helper was lost. Please retry the action.",
                    false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private AdminBrokerResult StartBroker()
    {
        var sid = WindowsIdentity.GetCurrent().User?.Value;
        if (string.IsNullOrWhiteSpace(sid))
            return new AdminBrokerResult(false, "Could not determine the current user.", false);

        var pipeName = BrokerProtocol.PipePrefix +
                       Environment.ProcessId.ToString() + "." + Guid.NewGuid().ToString("N");
        var exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppPilot.AdminBroker.exe");
        if (!File.Exists(exePath))
            return new AdminBrokerResult(
                false,
                "The elevated helper AppPilot.AdminBroker.exe was not found.",
                false);

        try
        {
            _broker = _launcher.Launch(exePath, pipeName, Environment.ProcessId, sid);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return new AdminBrokerResult(
                false,
                "Administrator permission was declined. No changes were made.",
                true);
        }
        catch (Exception ex)
        {
            return new AdminBrokerResult(false, $"Could not start the elevated helper: {ex.Message}", false);
        }

        if (_broker == null)
            return new AdminBrokerResult(false, "Could not start the elevated helper.", false);

        _pipe = _pipeFactory.Create(pipeName);
        return new AdminBrokerResult(true, "", false);
    }

    private void Reset()
    {
        _pipe?.Dispose();
        _pipe = null;

        if (_broker != null && _broker.Id != Environment.ProcessId)
        {
            try
            {
                if (!_broker.HasExited)
                    _broker.Kill();
            }
            catch (Exception)
            {
                // Best effort.
            }
        }

        _broker = null;
    }

    public void Dispose()
    {
        if (_pipe != null)
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
                _pipe.SendAsync(
                        new BrokerRequest { Operation = BrokerOperation.Shutdown },
                        timeout.Token)
                    .GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                // Best effort; the broker also exits when this process ends.
            }
        }

        Reset();
        _gate.Dispose();
    }
}

internal sealed class DefaultBrokerProcessLauncher : IBrokerProcessLauncher
{
    public Process? Launch(string exePath, string pipeName, int clientPid, string clientSid)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };

        startInfo.ArgumentList.Add("--pipe");
        startInfo.ArgumentList.Add(pipeName);
        startInfo.ArgumentList.Add("--client-pid");
        startInfo.ArgumentList.Add(clientPid.ToString());
        startInfo.ArgumentList.Add("--client-sid");
        startInfo.ArgumentList.Add(clientSid);

        return Process.Start(startInfo);
    }
}

internal sealed class DefaultBrokerPipeClientFactory : IBrokerPipeClientFactory
{
    public IBrokerPipeClient Create(string pipeName) => new BrokerPipeClient(pipeName);
}
