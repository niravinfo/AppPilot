using System.Collections.Generic;

namespace AppPilot.Models;

/// <summary>
/// Configuration for an npm command that can be run for a Node.js application.
/// </summary>
public class NpmCommandConfig
{
    /// <summary>
    /// Display name for the command (e.g., "Build", "Start", "Serve", "Preview").
    /// The first letter is shown on the button.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The npm script to run (e.g., "npm run build", "npm run start").
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// Creates default npm commands for React/Node.js applications.
    /// </summary>
    public static List<NpmCommandConfig> CreateDefaults()
    {
        return
        [
            new NpmCommandConfig { Name = "Build", Command = "npm run build" },
            new NpmCommandConfig { Name = "Start", Command = "npm run start" },
            new NpmCommandConfig { Name = "Preview", Command = "npm run preview" }
        ];
    }
}
