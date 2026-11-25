using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AppPilot.AdminBroker;
using AppPilot.Models;
using AppPilot.Services.ServiceControl;
using Xunit;

namespace AppPilot.Tests;

public class AdminBrokerClientTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task SendAsync_ReturnsBrokerResponse()
    {
        var pipeName = "AppPilot.AdminBroker.Test." + Guid.NewGuid().ToString("N");
        await using var server = new NamedPipeServerStream(
            pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

        var serverTask = Task.Run(async () =>
        {
            await server.WaitForConnectionAsync();
            using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
            using var writer = new StreamWriter(server, new UTF8Encoding(false), 1024, leaveOpen: true)
            {
                AutoFlush = true
            };

            var line = await reader.ReadLineAsync();
            var request = JsonSerializer.Deserialize<BrokerRequest>(line!, JsonOptions);

            Assert.NotNull(request);
            Assert.Equal(BrokerOperation.Start, request.Operation);
            Assert.Equal("MyWorker", request.Name);

            await writer.WriteLineAsync(JsonSerializer.Serialize(
                new BrokerResponse { Success = true }, JsonOptions));
        });

        using var client = new BrokerPipeClient(pipeName);
        var response = await client.SendAsync(
            new BrokerRequest { Operation = BrokerOperation.Start, Name = "MyWorker" },
            CancellationToken.None);

        Assert.True(response.Success);
        await serverTask;
    }

    [Fact]
    public async Task PipeFailure_ResetsBroker()
    {
        var failingPipe = new FakePipe(() => throw new IOException("pipe closed"));
        var workingPipe = new FakePipe(() => new BrokerResponse { Success = true });
        var factory = new FakePipeFactory(failingPipe, workingPipe);
        var launcher = new FakeLauncher(() => Process.GetCurrentProcess());

        using var client = new AdminBrokerClient(launcher, factory);
        var config = new ManagedServiceConfig { Name = "MyWorker", DisplayName = "My Worker" };

        var first = await client.ExecuteAsync(BrokerOperation.Start, config, CancellationToken.None);
        var second = await client.ExecuteAsync(BrokerOperation.Start, config, CancellationToken.None);

        Assert.False(first.Success);
        Assert.True(second.Success);
        Assert.Equal(2, factory.CreateCount);
    }

    [Fact]
    public async Task CanceledElevation_IsReportedWithoutStartingPipe()
    {
        var factory = new FakePipeFactory();
        var launcher = new FakeLauncher(() => throw new Win32Exception(1223));

        using var client = new AdminBrokerClient(launcher, factory);
        var config = new ManagedServiceConfig { Name = "MyWorker", DisplayName = "My Worker" };

        var result = await client.ExecuteAsync(BrokerOperation.Install, config, CancellationToken.None);

        Assert.True(result.WasCancelled);
        Assert.False(result.Success);
        Assert.Contains("declined", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, factory.CreateCount);
    }

    private sealed class FakeLauncher : IBrokerProcessLauncher
    {
        private readonly Func<Process> _start;

        public FakeLauncher(Func<Process> start) => _start = start;

        public int Calls { get; private set; }

        public Process? Launch(string exePath, string pipeName, int clientPid, string clientSid)
        {
            Calls++;
            return _start();
        }
    }

    private sealed class FakePipeFactory : IBrokerPipeClientFactory
    {
        private readonly Queue<IBrokerPipeClient> _clients;

        public FakePipeFactory(params IBrokerPipeClient[] clients) => _clients = new Queue<IBrokerPipeClient>(clients);

        public int CreateCount { get; private set; }

        public IBrokerPipeClient Create(string pipeName)
        {
            CreateCount++;
            return _clients.Dequeue();
        }
    }

    private sealed class FakePipe : IBrokerPipeClient
    {
        private readonly Func<BrokerResponse> _send;

        public FakePipe(Func<BrokerResponse> send) => _send = send;

        public Task<BrokerResponse> SendAsync(BrokerRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(_send());

        public void Dispose()
        {
        }
    }
}
