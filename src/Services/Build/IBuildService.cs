using System.Threading.Tasks;

namespace AppPilot.Services.Build;

public interface IBuildService
{
    /// <summary>
    /// Opens a visible PowerShell terminal that runs <c>dotnet build</c>.
    /// Closes automatically on success (exit 0); keeps window open on failure.
    /// Returns the process exit code: 0 = success.
    /// </summary>
    Task<int> LaunchBuildAsync(string projectPath, string displayName = "");
}
