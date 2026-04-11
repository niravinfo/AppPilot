using AppPilot.Domain.Enums;
using AppPilot.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AppPilot.Services.Discovery;

public class DiscoveryService : IServiceDiscoveryService
{
    public async Task<List<DiscoveredService>> DiscoverAsync(string rootDirectory)
    {
        return await Task.Run(() =>
        {
            var results = new List<DiscoveredService>();
            int displayOrder = 0;

            // Restrict search to 2 folder levels only (matching console tool behavior)
            var csprojFiles = Directory.GetFiles(rootDirectory, "*.csproj", SearchOption.TopDirectoryOnly)
                .Concat(Directory.GetDirectories(rootDirectory).SelectMany(d => Directory.GetFiles(d, "*.csproj", SearchOption.TopDirectoryOnly)))
                .Concat(Directory.GetDirectories(rootDirectory).SelectMany(d => Directory.GetDirectories(d).SelectMany(sd => Directory.GetFiles(sd, "*.csproj", SearchOption.TopDirectoryOnly))))
                .ToArray();

            foreach (var csproj in csprojFiles)
            {
                try
                {
                    var launchSettingsPath = Path.Combine(Path.GetDirectoryName(csproj)!, "Properties", "launchSettings.json");
                    if (!File.Exists(launchSettingsPath))
                    {
                        continue; // Only include projects with launchSettings.json
                    }

                    var projectName = Path.GetFileNameWithoutExtension(csproj);
                    var csprojText = File.ReadAllText(csproj);
                    string? tfm = null;

                    // Parse TargetFramework from csproj
                    var tfmMatch = Regex.Match(csprojText, @"<TargetFramework>([^<]+)</TargetFramework>", RegexOptions.IgnoreCase);
                    if (tfmMatch.Success)
                    {
                        tfm = tfmMatch.Groups[1].Value.Trim();
                    }

                    var binDebugDir = Path.Combine(Path.GetDirectoryName(csproj)!, "bin", "Debug");
                    string? exePath = null;
                    string? workingDir = null;
                    if (!string.IsNullOrEmpty(tfm))
                    {
                        var tfmDir = Path.Combine(binDebugDir, tfm);
                        if (Directory.Exists(tfmDir))
                        {
                            exePath = Path.Combine(tfmDir, projectName + ".exe");
                            workingDir = tfmDir;
                        }
                    }

                    displayOrder++;
                    var service = new DiscoveredService
                    {
                        ProjectPath = Path.GetDirectoryName(csproj)!,
                        ProjectName = projectName,
                        DisplayName = FormatDisplayName(projectName),
                        Type = ServiceType.WebApi, // default
                        ExecutablePath = exePath ?? string.Empty,
                        WorkingDirectory = workingDir ?? string.Empty,
                        CsprojPath = csproj,
                        Port = null,
                        HealthCheckUrl = string.Empty,
                        Arguments = string.Empty,
                        EnvironmentVariables = new Dictionary<string, string>(),
                        UseWindowsService = false,
                        GrpcEndpoint = null,
                        SwaggerUrl = null,
                        OpenApiPath = null,
                        IsSelected = true,
                        DisplayOrder = displayOrder,
                    };

                    // Detect Worker
                    if (csprojText.Contains("Microsoft.NET.Sdk.Worker", StringComparison.OrdinalIgnoreCase)
                        || (csprojText.Contains("Microsoft.NET.Sdk", StringComparison.OrdinalIgnoreCase)
                            && !csprojText.Contains("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase))
                        || (csprojText.Contains("<OutputType>Exe</OutputType>", StringComparison.OrdinalIgnoreCase)
                            && !csprojText.Contains("Microsoft.AspNetCore.App", StringComparison.OrdinalIgnoreCase)))
                    {
                        service.Type = ServiceType.Worker;
                        service.EnvironmentVariables["DOTNET_ENVIRONMENT"] = "Development";
                    }
                    else
                    {
                        // Detect gRPC by scanning Program.cs files
                        var dir = Path.GetDirectoryName(csproj)!;
                        var programFile = Directory.GetFiles(dir, "Program.cs", SearchOption.AllDirectories).FirstOrDefault();
                        if (programFile != null)
                        {
                            var programText = File.ReadAllText(programFile);
                            if (programText.Contains("MapGrpcService") || programText.Contains("AddGrpc"))
                            {
                                service.Type = ServiceType.Grpc;
                                service.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Development";
                                service.EnvironmentVariables["ASPNETCORE_Kestrel__Protocols"] = "Http2";
                            }
                            else
                            {
                                service.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Development";
                            }
                        }
                        else
                        {
                            service.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Development";
                        }
                    }

                    // Detect Windows Service hosting
                    //if (csprojText.Contains("Microsoft.Extensions.Hosting.WindowsServices", StringComparison.OrdinalIgnoreCase))
                    //{
                    //    service.UseWindowsService = true;
                    //}

                    // Extract ports from non-IIS profiles in launchSettings.json
                    int? httpsPort = null;
                    int? httpPort = null;
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
                                        var portMatch = Regex.Match(url, @":(\d+)");
                                        if (!portMatch.Success) continue;

                                        var portStr = portMatch.Groups[1].Value;
                                        if (!int.TryParse(portStr, out var port)) continue;

                                        if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                                        {
                                            httpsPort ??= port;
                                            service.Port = port;

                                            if (service.Type == ServiceType.Grpc && string.IsNullOrEmpty(service.GrpcEndpoint))
                                            {
                                                service.GrpcEndpoint = url;
                                            }

                                            if (service.Type == ServiceType.WebApi && string.IsNullOrEmpty(service.SwaggerUrl))
                                            {
                                                service.SwaggerUrl = $"{url.TrimEnd('/')}/swagger";
                                            }
                                        }
                                        else if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                                        {
                                            httpPort ??= port;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch { /* Ignore malformed JSON */ }

                    // Build --urls argument for API and gRPC services
                    if (service.Type == ServiceType.WebApi || service.Type == ServiceType.Grpc)
                    {
                        var preferredPort = httpsPort ?? httpPort;
                        if (preferredPort.HasValue)
                        {
                            var scheme = httpsPort.HasValue ? "https" : "http";
                            service.Arguments = $"--urls={scheme}://localhost:{preferredPort.Value}";
                        }
                    }

                    results.Add(service);
                }
                catch
                {
                    // Skip projects that fail to analyze
                }
            }

            return results;
        });
    }

    private static string FormatDisplayName(string projectName)
    {
        var result = Regex.Replace(projectName, "([a-z])([A-Z])", "$1 $2");
        result = Regex.Replace(result, "([A-Z]+)([A-Z][a-z])", "$1 $2");
        return result;
    }
}
