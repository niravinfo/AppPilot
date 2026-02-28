using System.Collections.Generic;

namespace AppPilot.Models;

public class AppSettings
{
    public AppPilotSettings AppPilot { get; set; } = new();
    public List<ManagedServiceConfig> Services { get; set; } = new();
}

public class AppPilotSettings
{
    public string ConfigurationPath { get; set; } = string.Empty;
    public int PollingIntervalMs { get; set; } = 3000;
    public bool AutoStartServices { get; set; }
    public bool MinimizeToTray { get; set; } = true;
    public string LogDirectory { get; set; } = "Logs";
}
