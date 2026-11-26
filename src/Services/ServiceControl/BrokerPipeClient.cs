using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AppPilot.AdminBroker;

namespace AppPilot.Services.ServiceControl;

public sealed class BrokerPipeClient : IBrokerPipeClient
{
    private const int ConnectTimeoutMs = 10_000;
    private const int CommandTimeoutSeconds = 60;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _pipeName;
    private NamedPipeClientStream? _stream;
    private StreamReader? _reader;
    private StreamWriter? _writer;

    public BrokerPipeClient(string pipeName) => _pipeName = pipeName;

    public async Task<BrokerResponse> SendAsync(BrokerRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (_stream == null)
            {
                _stream = new NamedPipeClientStream(
                    ".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                await _stream.ConnectAsync(ConnectTimeoutMs, cancellationToken);

                _reader = new StreamReader(_stream, Encoding.UTF8, leaveOpen: true);
                _writer = new StreamWriter(_stream, new UTF8Encoding(false), 1024, leaveOpen: true)
                {
                    AutoFlush = true
                };
            }

            var json = JsonSerializer.Serialize(request, JsonOptions);
            await _writer!.WriteLineAsync(json.AsMemory(), cancellationToken);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(CommandTimeoutSeconds));
            var line = await _reader!.ReadLineAsync(timeout.Token);

            if (line == null)
                throw new IOException("The elevated helper returned no response.");

            return JsonSerializer.Deserialize<BrokerResponse>(line, JsonOptions)
                   ?? throw new IOException("The elevated helper returned an invalid response.");
        }
        catch
        {
            Disconnect();
            throw;
        }
    }

    public void Dispose() => Disconnect();

    private void Disconnect()
    {
        _writer?.Dispose();
        _reader?.Dispose();
        _stream?.Dispose();
        _writer = null;
        _reader = null;
        _stream = null;
    }
}
