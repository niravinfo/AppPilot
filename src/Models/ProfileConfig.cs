using System.Collections.Generic;

namespace AppPilot.Models;

/// <summary>
/// Configuration for a service profile that groups a subset of services
/// for quick start/stop operations.
/// </summary>
public class ProfileConfig
{
    /// <summary>
    /// Unique identifier for the profile.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the profile.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description for the profile.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Whether this profile should be loaded by default on startup.
    /// Only one profile can have this flag set to true.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Display order for sorting profiles in the UI.
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// List of service names included in this profile, in display order.
    /// </summary>
    public List<string> ServiceNames { get; set; } = [];

    public override string ToString() => Name;

    /// <summary>
    /// Ensures properties are never null after deserialization.
    /// </summary>
    public void EnsureNotNull()
    {
        Id ??= string.Empty;
        Name ??= string.Empty;
        Description ??= string.Empty;
        ServiceNames ??= [];
    }
}
