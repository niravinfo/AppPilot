using System.Threading.Tasks;

namespace AppPilot.Services.Update;

public interface IUpdateService
{
    /// <summary>Current installed version string, e.g. "1.2.3".</summary>
    string CurrentVersion { get; }

    /// <summary>
    /// True when running inside a Velopack installation (i.e. launched via Setup.exe).
    /// False when running the raw .exe directly (e.g. from bin\Debug).
    /// Update checks will still run but ApplyUpdateAndRestart cannot work outside a Velopack install.
    /// </summary>
    bool IsVelopackEnvironment { get; }

    /// <summary>True while a check or download is in progress.</summary>
    bool IsBusy { get; }

    /// <summary>
    /// Checks GitHub Releases for a newer version.
    /// Returns the new version string if available, otherwise null.
    /// </summary>
    Task<string?> CheckForUpdateAsync();

    /// <summary>
    /// Downloads and stages the latest update, then restarts into the new version.
    /// Call only after <see cref="CheckForUpdateAsync"/> returned a non-null value.
    /// </summary>
    Task ApplyUpdateAndRestartAsync();
}
