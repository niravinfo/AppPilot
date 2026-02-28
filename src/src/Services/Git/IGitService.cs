using System.Threading.Tasks;

namespace AppPilot.Services.Git;

public interface IGitService
{
    Task<(bool Success, string Output)> PullAsync(string repoPath);
    Task<string> GetCurrentBranchAsync(string repoPath);
    Task<string> GetLastCommitAsync(string repoPath);
}
