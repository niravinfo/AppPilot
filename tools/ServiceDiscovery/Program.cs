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
    public string ExecutablePath { get; set; }
    public string WorkingDirectory { get; set; }
    public Dictionary<string, string> SuggestedEnvironment { get; set; }
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

            var projectName = System.IO.Path.GetFileNameWithoutExtension(csproj);
            var csprojText = File.ReadAllText(csproj);
            string tfm = null;

            // Parse TargetFramework from csproj
            var tfmMatch = Regex.Match(csprojText, @"<TargetFramework>([^<]+)</TargetFramework>", RegexOptions.IgnoreCase);
            if (tfmMatch.Success)
            {
                tfm = tfmMatch.Groups[1].Value.Trim();
            }

            var binDebugDir = Path.Combine(Path.GetDirectoryName(csproj), "bin", "Debug");
            string exePath = null;
            string workingDir = null;
            if (!string.IsNullOrEmpty(tfm))
            {
                var tfmDir = Path.Combine(binDebugDir, tfm);
                if (Directory.Exists(tfmDir))
                {
                    exePath = Path.Combine(tfmDir, projectName + ".exe");
                    workingDir = tfmDir;
                }
            }

            var info = new ServiceInfo
            {
                Name = projectName,
                Path = csproj,
                Type = "API",
                Ports = new List<string>(),
                ExecutablePath = exePath ?? string.Empty,
                WorkingDirectory = workingDir ?? string.Empty,
                SuggestedEnvironment = new Dictionary<string, string>()
            };

            // Detect Worker
            if (csprojText.Contains("Microsoft.NET.Sdk.Worker", StringComparison.OrdinalIgnoreCase)
                || (csprojText.Contains("Microsoft.NET.Sdk", StringComparison.OrdinalIgnoreCase)
                    && !csprojText.Contains("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase))
                || (csprojText.Contains("<OutputType>Exe</OutputType>", StringComparison.OrdinalIgnoreCase)
                    && !csprojText.Contains("Microsoft.AspNetCore.App", StringComparison.OrdinalIgnoreCase)))
            {
                info.Type = "Worker";
                info.SuggestedEnvironment["DOTNET_ENVIRONMENT"] = "Development";
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
                        info.SuggestedEnvironment["ASPNETCORE_ENVIRONMENT"] = "Development";
                        info.SuggestedEnvironment["ASPNETCORE_Kestrel__Protocols"] = "Http2";
                    }
                    else
                    {
                        info.SuggestedEnvironment["ASPNETCORE_ENVIRONMENT"] = "Development";
                    }
                }
                else
                {
                    info.SuggestedEnvironment["ASPNETCORE_ENVIRONMENT"] = "Development";
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
                        {
                            continue;
                        }

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
