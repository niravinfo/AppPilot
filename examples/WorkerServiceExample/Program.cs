using System.Runtime.InteropServices;
using WorkerServiceExample;

IHostBuilder builder = Host.CreateDefaultBuilder(args);

// check for windows
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    builder.UseWindowsService();
}

builder.ConfigureServices(services =>
{
    services.AddHostedService<Worker>();

});

var host = builder.Build();
await host.RunAsync();
