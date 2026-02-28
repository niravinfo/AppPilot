using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace AppPilot.Services.Git;

public class GitService : IGitService
{
    public Task<(bool Success, string Output)> PullAsync(string repoPath) =>
        RunAsync(repoPath, "pull");

    public async Task<string> GetCurrentBranchAsync(string repoPath)
    {
        var (success, output) = await RunAsync(repoPath, "rev-parse", "--abbrev-ref", "HEAD");
        return success ? output.Trim() : "unknown";
    }

    public async Task<string> GetLastCommitAsync(string repoPath)
    {
        var (success, output) = await RunAsync(repoPath, "log", "-1", "--pretty=format:%h %s");
        return success ? output.Trim() : string.Empty;
    }

    private static async Task<(bool Success, string Output)> RunAsync(string workingDir, params string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        try
        {
            using var proc = Process.Start(psi)!;
            var stdout = await proc.StandardOutput.ReadToEndAsync();
            var stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            return proc.ExitCode == 0
                ? (true, stdout)
                : (false, stderr.Trim());
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
