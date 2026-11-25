using System;
using System.Threading;
using System.Threading.Tasks;
using AppPilot.AdminBroker;
using AppPilot.Models;
using AppPilot.Services.ServiceControl;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AppPilot.Tests;

public class WindowsServiceControllerTests
{
    [Fact]
    public async Task InstallAsync_DelegatesInstall()
    {
        var config = new ManagedServiceConfig
        {
            Name = "MyWorker",
            DisplayName = "My Worker",
            ExecutablePath = @"C:\apps\worker.exe"
        };
        var broker = new FakeBroker(new AdminBrokerResult(true, "", false));
        var controller = new WindowsServiceController(NullLogger<WindowsServiceController>.Instance, broker);

        var result = await controller.InstallAsync(config);

        Assert.True(result);
        Assert.Equal(BrokerOperation.Install, broker.Operation);
        Assert.Same(config, broker.Config);
    }

    [Fact]
    public async Task StartAsync_BrokerFailure_Throws()
    {
        var config = new ManagedServiceConfig { Name = "MyWorker" };
        var broker = new FakeBroker(new AdminBrokerResult(false, "sc.exe failed", false));
        var controller = new WindowsServiceController(NullLogger<WindowsServiceController>.Instance, broker);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => controller.StartAsync(config));

        Assert.Contains("sc.exe failed", exception.Message, StringComparison.Ordinal);
        Assert.Equal(BrokerOperation.Start, broker.Operation);
    }

    [Fact]
    public async Task StopAsync_DeclinedElevation_ThrowsWithClearMessage()
    {
        var config = new ManagedServiceConfig { Name = "MyWorker" };
        var broker = new FakeBroker(new AdminBrokerResult(false, "declined", true));
        var controller = new WindowsServiceController(NullLogger<WindowsServiceController>.Instance, broker);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => controller.StopAsync(config));

        Assert.Contains("Administrator permission was declined", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetStatus_DoesNotTouchBroker()
    {
        var config = new ManagedServiceConfig { Name = "AppPilot.Test.Service.ThatDoesNotExist" };
        var broker = new FakeBroker(new AdminBrokerResult(true, "", false));
        var controller = new WindowsServiceController(NullLogger<WindowsServiceController>.Instance, broker);

        controller.GetStatus(config);

        Assert.Equal(0, broker.Calls);
    }

    private sealed class FakeBroker : IAdminBrokerClient
    {
        private readonly AdminBrokerResult _result;

        public FakeBroker(AdminBrokerResult result) => _result = result;

        public int Calls { get; private set; }
        public BrokerOperation? Operation { get; private set; }
        public ManagedServiceConfig? Config { get; private set; }

        public Task<AdminBrokerResult> ExecuteAsync(
            BrokerOperation operation,
            ManagedServiceConfig config,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            Operation = operation;
            Config = config;
            return Task.FromResult(_result);
        }

        public void Dispose()
        {
        }
    }
}
