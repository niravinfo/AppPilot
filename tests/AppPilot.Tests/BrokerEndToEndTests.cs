using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using AppPilot.AdminBroker;
using AppPilot.Services.ServiceControl;
using Xunit;

namespace AppPilot.Tests;

public class BrokerEndToEndTests
{
    [Fact]
    public async Task RealBroker_RejectsBadRequests_AndShutsDownCleanly()
    {
        var pipeName = BrokerProtocol.PipePrefix + "e2e." + Guid.NewGuid().ToString("N");
        var sid = WindowsIdentity.GetCurrent().User?.Value;
        Assert.False(string.IsNullOrWhiteSpace(sid));

        var brokerDll = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppPilot.AdminBroker.dll");
        Assert.True(File.Exists(brokerDll), "Broker output was not copied to the test directory");

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(brokerDll);
        startInfo.ArgumentList.Add("--pipe");
        startInfo.ArgumentList.Add(pipeName);
        startInfo.ArgumentList.Add("--client-pid");
        startInfo.ArgumentList.Add(Environment.ProcessId.ToString());
        startInfo.ArgumentList.Add("--client-sid");
        startInfo.ArgumentList.Add(sid!);

        using var broker = Process.Start(startInfo);
        Assert.NotNull(broker);

        try
        {
            using var client = new BrokerPipeClient(pipeName);

            var unknown = await client.SendAsync(
                new BrokerRequest { Operation = (BrokerOperation)99, Name = "AnyWorker" },
                CancellationToken.None);
            Assert.False(unknown.Success);
            Assert.Equal("unsupported_operation", unknown.Error);

            var invalidName = await client.SendAsync(
                new BrokerRequest { Operation = BrokerOperation.Start, Name = "bad;name" },
                CancellationToken.None);
            Assert.False(invalidName.Success);
            Assert.Equal("invalid_service_name", invalidName.Error);

            var shutdown = await client.SendAsync(
                new BrokerRequest { Operation = BrokerOperation.Shutdown },
                CancellationToken.None);
            Assert.True(shutdown.Success);

            var exitTask = broker.WaitForExitAsync();
            var completed = await Task.WhenAny(exitTask, Task.Delay(10_000));
            Assert.Equal(exitTask, completed);
        }
        finally
        {
            try
            {
                if (!broker.HasExited)
                    broker.Kill();
            }
            catch (Exception)
            {
                // Best effort.
            }
        }
    }
}
