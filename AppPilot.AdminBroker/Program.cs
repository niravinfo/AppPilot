using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace AppPilot.AdminBroker;

internal static class NativeMethods
{
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool GetNamedPipeClientProcessId(IntPtr pipe, out uint clientProcessId);
}

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static async Task<int> Main(string[] args)
    {
        if (!TryParseArgs(args, out var pipeName, out var clientPid, out var clientSid))
            return 2;

        SecurityIdentifier callerSid;
        try
        {
            callerSid = new SecurityIdentifier(clientSid!);
        }
        catch (Exception)
        {
            return 2;
        }

        var pipeSecurity = new PipeSecurity();
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            callerSid, PipeAccessRights.ReadWrite, AccessControlType.Allow));

        await using var server = NamedPipeServerStreamAcl.Create(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            0, 0,
            pipeSecurity);

        var connectTask = server.WaitForConnectionAsync();
        if (await Task.WhenAny(connectTask, Task.Delay(TimeSpan.FromSeconds(30))) != connectTask)
            return 3;
        await connectTask;

        if (!TryGetClientProcessId(server, out var connectedPid) || connectedPid != clientPid)
            return 4;

        using var watchCts = new CancellationTokenSource();
        var watcher = WatchParentProcessAsync(clientPid, server, watchCts.Token);

        using var reader = new StreamReader(server, Encoding.UTF8, false, 1024, leaveOpen: true);
        using var writer = new StreamWriter(server, new UTF8Encoding(false), 1024, leaveOpen: true)
        {
            AutoFlush = true
        };

        while (true)
        {
            string? line;
            try
            {
                line = await reader.ReadLineAsync();
            }
            catch (IOException)
            {
                break;
            }

            if (line == null)
                break;

            BrokerRequest? request;
            try
            {
                request = JsonSerializer.Deserialize<BrokerRequest>(line, JsonOptions);
            }
            catch (JsonException)
            {
                request = null;
            }

            if (request == null)
            {
                await WriteResponseAsync(writer, new BrokerResponse
                {
                    Success = false,
                    Error = "invalid_request"
                });
                continue;
            }

            if (request.Operation == BrokerOperation.Shutdown)
                break;

            var response = await ServiceOperations.ExecuteAsync(request);
            await WriteResponseAsync(writer, response);
        }

        watchCts.Cancel();
        try
        {
            await watcher;
        }
        catch (OperationCanceledException)
        {
            // Expected when the parent is still alive.
        }

        return 0;
    }

    private static bool TryGetClientProcessId(NamedPipeServerStream server, out int processId)
    {
        processId = 0;
        try
        {
            var handle = server.SafePipeHandle.DangerousGetHandle();
            if (!NativeMethods.GetNamedPipeClientProcessId(handle, out var nativePid))
                return false;

            processId = unchecked((int)nativePid);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static async Task WatchParentProcessAsync(
        int parentPid,
        NamedPipeServerStream server,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(1000, cancellationToken);

            try
            {
                using var parent = Process.GetProcessById(parentPid);
                if (!parent.HasExited)
                    continue;
            }
            catch (ArgumentException)
            {
                // Parent no longer exists.
            }
            catch (InvalidOperationException)
            {
                break;
            }

            try
            {
                server.Disconnect();
            }
            catch (Exception)
            {
                // Already disconnected.
            }
            break;
        }
    }

    private static async Task WriteResponseAsync(StreamWriter writer, BrokerResponse response)
    {
        try
        {
            await writer.WriteLineAsync(
                JsonSerializer.Serialize(response, JsonOptions));
        }
        catch (IOException)
        {
            // Client went away; the loop will exit on the next read.
        }
    }

    private static bool TryParseArgs(
        string[] args,
        out string? pipeName,
        out int clientPid,
        out string? clientSid)
    {
        pipeName = null;
        clientPid = 0;
        clientSid = null;

        for (var i = 0; i + 1 < args.Length; i += 2)
        {
            switch (args[i])
            {
                case "--pipe":
                    pipeName = args[i + 1];
                    break;
                case "--client-pid":
                    if (!int.TryParse(args[i + 1], out clientPid))
                        return false;
                    break;
                case "--client-sid":
                    clientSid = args[i + 1];
                    break;
                default:
                    return false;
            }
        }

        return !string.IsNullOrWhiteSpace(pipeName) &&
               clientPid > 0 &&
               !string.IsNullOrWhiteSpace(clientSid);
    }
}
