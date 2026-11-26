using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace AppPilot.AdminBroker;

public static class ServiceOperations
{
    private const int TimeoutSeconds = 30;

    public static async Task<BrokerResponse> ExecuteAsync(
        BrokerRequest request,
        Func<string, ServiceControllerStatus?>? getStatus = null,
        Func<IEnumerable<string>, Task<(bool Success, string Error)>>? runSc = null)
    {
        getStatus ??= GetStatus;
        runSc ??= RunScAsync;

        if (!Enum.IsDefined(request.Operation))
            return Failure("unsupported_operation");

        if (request.Operation != BrokerOperation.Shutdown &&
            !BrokerProtocol.IsValidServiceName(request.Name))
            return Failure("invalid_service_name");

        return request.Operation switch
        {
            BrokerOperation.Install => await InstallAsync(request, runSc),
            BrokerOperation.Uninstall => await UninstallAsync(request, getStatus, runSc),
            BrokerOperation.Start => await StartAsync(request, getStatus, runSc),
            BrokerOperation.Stop => await StopAsync(request, getStatus, runSc),
            _ => Failure("unsupported_operation")
        };
    }

    private static async Task<BrokerResponse> InstallAsync(
        BrokerRequest request,
        Func<IEnumerable<string>, Task<(bool Success, string Error)>> runSc)
    {
        var (success, error) = await runSc(ScArgumentBuilder.Create(
            request.Name, request.ExecutablePath, request.Arguments));
        if (!success)
            return Failure(error);

        // A missing description does not prevent the service from working.
        await runSc(ScArgumentBuilder.Description(request.Name, request.DisplayName));
        return Success();
    }

    private static async Task<BrokerResponse> UninstallAsync(
        BrokerRequest request,
        Func<string, ServiceControllerStatus?> getStatus,
        Func<IEnumerable<string>, Task<(bool Success, string Error)>> runSc)
    {
        var status = getStatus(request.Name);
        if (status is ServiceControllerStatus.Running or ServiceControllerStatus.StartPending)
        {
            var (stopSuccess, stopError) = await runSc(ScArgumentBuilder.Stop(request.Name));
            if (!stopSuccess)
                return Failure(stopError);

            var stopped = await WaitForStatusAsync(
                request.Name, ServiceControllerStatus.Stopped, getStatus);
            if (!stopped.Success)
                return stopped;
        }

        var (success, error) = await runSc(ScArgumentBuilder.Delete(request.Name));
        return success ? Success() : Failure(error);
    }

    private static async Task<BrokerResponse> StartAsync(
        BrokerRequest request,
        Func<string, ServiceControllerStatus?> getStatus,
        Func<IEnumerable<string>, Task<(bool Success, string Error)>> runSc)
    {
        var status = getStatus(request.Name);
        if (status == ServiceControllerStatus.Running)
            return Success();

        var (success, error) = await runSc(ScArgumentBuilder.Start(request.Name));
        if (!success)
            return Failure(error);

        return await WaitForStatusAsync(
            request.Name,
            ServiceControllerStatus.Running,
            getStatus,
            failIfStopped: true);
    }

    private static async Task<BrokerResponse> StopAsync(
        BrokerRequest request,
        Func<string, ServiceControllerStatus?> getStatus,
        Func<IEnumerable<string>, Task<(bool Success, string Error)>> runSc)
    {
        var status = getStatus(request.Name);
        if (status == null)
            return Failure("service_not_found");

        if (status == ServiceControllerStatus.Stopped)
            return Success();

        var (success, error) = await runSc(ScArgumentBuilder.Stop(request.Name));
        if (!success)
            return Failure(error);

        return await WaitForStatusAsync(
            request.Name, ServiceControllerStatus.Stopped, getStatus);
    }

    private static async Task<BrokerResponse> WaitForStatusAsync(
        string name,
        ServiceControllerStatus target,
        Func<string, ServiceControllerStatus?> getStatus,
        bool failIfStopped = false)
    {
        var deadline = DateTime.UtcNow.AddSeconds(TimeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(300);
            var status = getStatus(name);

            if (status == target)
                return Success();

            if (failIfStopped && status == ServiceControllerStatus.Stopped)
                return Failure(
                    $"'{name}' stopped immediately after launching. " +
                    "Ensure the project calls UseWindowsService() and references " +
                    "Microsoft.Extensions.Hosting.WindowsServices.");
        }

        return Failure($"'{name}' did not reach {target} within {TimeoutSeconds} seconds.");
    }

    private static ServiceControllerStatus? GetStatus(string name)
    {
        try
        {
            using var controller = new ServiceController(name);
            controller.Refresh();
            return controller.Status;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static async Task<(bool Success, string Error)> RunScAsync(IEnumerable<string> arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "sc.exe",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            using var process = Process.Start(startInfo);
            if (process == null)
                return (false, "Failed to launch sc.exe.");

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var output = await outputTask;
            var error = await errorTask;

            if (process.ExitCode == 0)
                return (true, "");

            var message = !string.IsNullOrWhiteSpace(error) ? error.Trim()
                : !string.IsNullOrWhiteSpace(output) ? output.Trim()
                : $"sc.exe exited with code {process.ExitCode}.";
            return (false, message);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static BrokerResponse Success() => new() { Success = true };

    private static BrokerResponse Failure(string error) => new() { Success = false, Error = error };
}
