using AppPilot.Domain.Enums;
using System.Collections.Generic;

namespace AppPilot.Models;

public class ManagedServiceConfig
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string GroupId { get; set; } = string.Empty;
    public ServiceType Type { get; set; }
    public string ExecutablePath { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public int? Port { get; set; }
    public string HealthCheckUrl { get; set; } = string.Empty;
    public bool AutoStart { get; set; }
    public int StartOrder { get; set; }
    public List<string> Dependencies { get; set; } = new();
    public Dictionary<string, string> Environment { get; set; } = new();

    /// <summary>
    /// Path to the .csproj for the Build command. Relative to BasePath or absolute.
    /// </summary>
    public string CsprojPath { get; set; } = string.Empty;

    /// <summary>
    /// If true, run/install as Windows Service. If false, run as regular process.
    /// </summary>
    public bool UseWindowsService { get; set; } = false;
}
