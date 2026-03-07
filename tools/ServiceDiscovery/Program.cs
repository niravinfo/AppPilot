using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace ServiceDiscovery;

public class ServiceInfo
{
    public string Name { get; set; }
    public string Path { get; set; }
    public string Type { get; set; }
    public List<string> Ports { get; set; }
}

class Program
{
    static void Main(string[] args)
    {
        string rootDir = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
        var services = DiscoverServices(rootDir);
        Console.WriteLine(JsonSerializer.Serialize(services, new JsonSerializerOptions { WriteIndented = true }));
    }

    static List<ServiceInfo> DiscoverServices(string rootDir)
    {
        // Restrict search to 2 folder levels only
        var csprojFiles = Directory.GetFiles(rootDir, "*.csproj", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetDirectories(rootDir).SelectMany(d => Directory.GetFiles(d, "*.csproj", SearchOption.TopDirectoryOnly)))
            .Concat(Directory.GetDirectories(rootDir).SelectMany(d => Directory.GetDirectories(d).SelectMany(sd => Directory.GetFiles(sd, "*.csproj", SearchOption.TopDirectoryOnly))))
            .ToArray();
        var services = new List<ServiceInfo>();
        foreach (var csproj in csprojFiles)
        {
            var launchSettingsPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(csproj), "Properties", "launchSettings.json");
            if (!File.Exists(launchSettingsPath))
            {
                continue; // Only include projects with launchSettings.json
            }

            var info = new ServiceInfo
            {
                Name = System.IO.Path.GetFileNameWithoutExtension(csproj),
                Path = csproj,
                Type = "API",
                Ports = new List<string>()
            };

            // Detect Worker
            var csprojText = File.ReadAllText(csproj);
            if (csprojText.Contains("Microsoft.NET.Sdk.Worker", StringComparison.OrdinalIgnoreCase)
                || (csprojText.Contains("Microsoft.NET.Sdk", StringComparison.OrdinalIgnoreCase) && !csprojText.Contains("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase))
                || (csprojText.Contains("<OutputType>Exe</OutputType>", StringComparison.OrdinalIgnoreCase) && !csprojText.Contains("Microsoft.AspNetCore.App", StringComparison.OrdinalIgnoreCase)))
            {
                info.Type = "Worker";
            }
            else
            {
                // Detect gRPC
                var dir = System.IO.Path.GetDirectoryName(csproj);
                var programFile = Directory.GetFiles(dir, "Program.cs", SearchOption.AllDirectories).FirstOrDefault();
                if (programFile != null)
                {
                    var programText = File.ReadAllText(programFile);
                    if (programText.Contains("MapGrpcService") || programText.Contains("AddGrpc"))
                    {
                        info.Type = "gRPC";
                    }
                }
            }

            // Extract only HTTPS port from non-IIS profiles in launchSettings.json
            var launchText = File.ReadAllText(launchSettingsPath);
            try
            {
                using var doc = JsonDocument.Parse(launchText);
                if (doc.RootElement.TryGetProperty("profiles", out var profiles))
                {
                    foreach (var profile in profiles.EnumerateObject())
                    {
                        var profileName = profile.Name;
                        if (profileName.Contains("IIS", StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (profile.Value.TryGetProperty("applicationUrl", out var appUrlProp))
                        {
                            var urls = appUrlProp.GetString()?.Split(';') ?? Array.Empty<string>();
                            foreach (var url in urls)
                            {
                                if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                                {
                                    var portMatch = Regex.Match(url, @":(\d+)");
                                    if (portMatch.Success)
                                    {
                                        info.Ports.Add(portMatch.Groups[1].Value);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { /* Ignore malformed JSON */ }

            services.Add(info);
        }
        return services;
    }
}
