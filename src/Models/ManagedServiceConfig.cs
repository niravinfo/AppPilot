using AppPilot.Domain.Enums;
using System.Collections.Generic;

namespace AppPilot.Models;

public class ManagedServiceConfig
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
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
}
