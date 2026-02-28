using System.Collections.Generic;

namespace AppPilot.Models;

public class AppSettings
{
    public AppPilotSettings AppPilot { get; set; } = new();
    public List<ManagedServiceConfig> Services { get; set; } = new();
    public List<GitRepositoryConfig> GitRepositories { get; set; } = [];
}

public class AppPilotSettings
{
    public string BasePath { get; set; } = string.Empty;
    public string ConfigurationPath { get; set; } = string.Empty;
    public int PollingIntervalMs { get; set; } = 3000;
    public bool AutoStartServices { get; set; }
    public bool MinimizeToTray { get; set; } = true;
    public string LogDirectory { get; set; } = "Logs";

    /// <summary>GitHub repository URL used for update checks, e.g. "https://github.com/owner/repo".</summary>
    public string GitHubRepoUrl { get; set; } = string.Empty;

    /// <summary>
    /// GitHub personal access token (PAT) with <c>repo</c> read scope.
    /// Required for private repositories. Leave empty for public repos.
    /// Store this in appsettings.Local.json (gitignored), not in appsettings.json.
    /// </summary>
    public string GitHubToken { get; set; } = string.Empty;

    /// <summary>When true, AppPilot silently checks for a new release on startup.</summary>
    public bool CheckForUpdatesOnStartup { get; set; } = true;
}
