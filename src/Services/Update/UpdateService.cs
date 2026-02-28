using Serilog;
using System;
using System.Threading.Tasks;
using Velopack;
using Velopack.Locators;
using Velopack.Sources;

namespace AppPilot.Services.Update;

public class UpdateService : IUpdateService
{
    private readonly ILogger _logger;
    private readonly string _githubRepoUrl;
    private readonly string? _githubToken;
    private UpdateInfo? _pendingUpdate;

    public string CurrentVersion { get; }
    public bool IsVelopackEnvironment { get; }
    public bool IsBusy { get; private set; }

    /// <param name="githubToken">
    /// Personal access token with <c>repo</c> read scope.
    /// Required for private repositories; leave null for public repos.
    /// </param>
    public UpdateService(ILogger logger, string githubRepoUrl, string? githubToken = null)
    {
        _logger = logger;
        _githubRepoUrl = githubRepoUrl;
        _githubToken = githubToken;

        var locator = VelopackLocator.IsCurrentSet ? VelopackLocator.Current : null;
        IsVelopackEnvironment = locator?.CurrentlyInstalledVersion != null;
        CurrentVersion = locator?.CurrentlyInstalledVersion?.ToString()
            ?? GetFallbackVersion();

        if (!IsVelopackEnvironment)
            _logger.Warning(
                "UpdateService: not running inside a Velopack installation (raw .exe). " +
                "Update checks will run but ApplyUpdateAndRestart will not work. " +
                "Install the app via Setup.exe to enable full update support.");
    }

    public async Task<string?> CheckForUpdateAsync()
    {
        if (string.IsNullOrWhiteSpace(_githubRepoUrl))
        {
            _logger.Warning("UpdateService: GitHubRepoUrl is not configured, skipping update check.");
            return null;
        }

        IsBusy = true;
        try
        {
            _logger.Information("Checking for updates from {Url}", _githubRepoUrl);
            var mgr = CreateUpdateManager();
            _pendingUpdate = await mgr.CheckForUpdatesAsync();

            if (_pendingUpdate is null)
            {
                _logger.Information(
                    "No update available. This means either: " +
                    "(1) no releases exist on GitHub yet, or " +
                    "(2) current version {Version} is already the latest.",
                    CurrentVersion);
                return null;
            }

            var newVersion = _pendingUpdate.TargetFullRelease.Version.ToString();
            _logger.Information("Update available: {Version}", newVersion);
            return newVersion;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to check for updates");
            return null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ApplyUpdateAndRestartAsync()
    {
        if (_pendingUpdate is null)
        {
            _logger.Warning("ApplyUpdateAndRestartAsync called but no pending update — running CheckForUpdateAsync first");
            await CheckForUpdateAsync();
            if (_pendingUpdate is null) return;
        }

        IsBusy = true;
        try
        {
            _logger.Information("Downloading and applying update {Version}", _pendingUpdate.TargetFullRelease.Version);
            var mgr = CreateUpdateManager();
            await mgr.DownloadUpdatesAsync(_pendingUpdate);
            mgr.ApplyUpdatesAndRestart(_pendingUpdate);
        }
        catch (Exception ex)
        {
            IsBusy = false;
            _logger.Error(ex, "Failed to apply update");
            throw;
        }
    }

    private UpdateManager CreateUpdateManager()
        => new(new GithubSource(_githubRepoUrl, _githubToken, false));

    private static string GetFallbackVersion()
    {
        var asm = System.Reflection.Assembly.GetExecutingAssembly();
        return asm.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
