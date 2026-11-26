using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading.Tasks;
using AppPilot.AdminBroker;
using Xunit;

namespace AppPilot.Tests;

public class ServiceOperationsTests
{
    [Fact]
    public async Task StartAsync_ScFailure_ReturnsErrorWithoutWaiting()
    {
        var request = new BrokerRequest
        {
            Operation = BrokerOperation.Start,
            Name = "MyWorker",
            DisplayName = "My Worker"
        };
        var stopwatch = Stopwatch.StartNew();

        var result = await ServiceOperations.ExecuteAsync(
            request,
            _ => ServiceControllerStatus.Stopped,
            _ => Task.FromResult((false, "access denied")));

        stopwatch.Stop();
        Assert.False(result.Success);
        Assert.Contains("access denied", result.Error, StringComparison.Ordinal);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), "sc.exe failure was not surfaced immediately");
    }

    [Fact]
    public async Task StopAsync_MissingService_ReturnsFailure()
    {
        var request = new BrokerRequest
        {
            Operation = BrokerOperation.Stop,
            Name = "MissingWorker"
        };
        var runScCalled = false;

        var result = await ServiceOperations.ExecuteAsync(
            request,
            _ => null,
            _ =>
            {
                runScCalled = true;
                return Task.FromResult((true, ""));
            });

        Assert.False(result.Success);
        Assert.Contains("service_not_found", result.Error, StringComparison.Ordinal);
        Assert.False(runScCalled);
    }
}
