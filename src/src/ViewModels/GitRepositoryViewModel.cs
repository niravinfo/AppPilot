using AppPilot.Domain.Enums;
using AppPilot.Models;
using AppPilot.Services.Build;
using AppPilot.Services.Git;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AppPilot.ViewModels;

public partial class GitRepositoryViewModel : ViewModelBase
{
    private readonly IBuildService _buildService;
    private readonly IGitService _gitService;
    private readonly ILogger _logger;

    public GitRepositoryConfig Config { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PullCommand))]
    [NotifyCanExecuteChangedFor(nameof(BuildSolutionCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _currentBranch = "…";

    [ObservableProperty]
    private string _lastCommit = string.Empty;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _output = string.Empty;

    [ObservableProperty]
    private bool _isOutputVisible;

    [ObservableProperty]
    private bool _lastOperationFailed;

    public string Name      => Config.DisplayName;
    public string LocalPath => Config.LocalPath;
    public bool HasSolution => !string.IsNullOrWhiteSpace(Config.SolutionPath);
    public bool HasLinkedServices => LinkedServices.Count > 0;

    public ObservableCollection<ServiceItemViewModel> LinkedServices { get; } = [];

    public GitRepositoryViewModel(
        GitRepositoryConfig config,
        IBuildService buildService,
        IGitService gitService,
        ILogger logger)
    {
        Config = config;
        _buildService = buildService;
        _gitService = gitService;
        _logger = logger;

        LinkedServices.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasLinkedServices));
    }

    public async Task InitializeAsync()
    {
        try
        {
            CurrentBranch = await _gitService.GetCurrentBranchAsync(Config.LocalPath);
            LastCommit    = await _gitService.GetLastCommitAsync(Config.LocalPath);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Could not load git info for {Repo}", Config.Name);
            CurrentBranch = "—";
        }
    }

    // ── Pull ─────────────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task PullAsync()
    {
        IsBusy = true;
        StatusText = "Pulling…";
        Output = string.Empty;
        IsOutputVisible = true;
        LastOperationFailed = false;

        try
        {
            var (success, output) = await _gitService.PullAsync(Config.LocalPath);
            Output = output.Trim();

            if (success)
            {
                StatusText = "Pull succeeded";
                CurrentBranch = await _gitService.GetCurrentBranchAsync(Config.LocalPath);
                LastCommit    = await _gitService.GetLastCommitAsync(Config.LocalPath);
            }
            else
            {
                StatusText = "Pull failed";
                LastOperationFailed = true;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Git pull failed for {Repo}", Config.Name);
            Output = ex.Message;
            StatusText = "Pull error";
            LastOperationFailed = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ── Build solution ────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanBuildSolution))]
    private async Task BuildSolutionAsync()
    {
        IsBusy = true;
        LastOperationFailed = false;

        var stoppedServices = LinkedServices
            .Where(s => s.Status == ServiceStatus.Running)
            .ToList();

        if (stoppedServices.Count > 0)
        {
            StatusText = $"Stopping {stoppedServices.Count} service(s)…";
            foreach (var svc in stoppedServices)
            {
                await svc.StopAsync();
                await Task.Delay(300);
            }
        }

        StatusText = "Building solution…";
        var solutionName = Path.GetFileNameWithoutExtension(Config.SolutionPath);
        var exitCode = await _buildService.LaunchBuildAsync(Config.SolutionPath, solutionName);

        if (exitCode == 0)
        {
            if (stoppedServices.Count > 0)
            {
                StatusText = $"Build succeeded — restarting {stoppedServices.Count} service(s)…";
                foreach (var svc in stoppedServices)
                {
                    await svc.StartAsync();
                    await Task.Delay(400);
                }
            }
            StatusText = "Build succeeded";
        }
        else
        {
            StatusText = stoppedServices.Count > 0
                ? $"Build failed — {stoppedServices.Count} service(s) remain stopped"
                : "Build failed";
            LastOperationFailed = true;
        }

        IsBusy = false;
    }

    // ── Toggle output panel ───────────────────────────────────────────────────

    [RelayCommand]
    private void ToggleOutput() => IsOutputVisible = !IsOutputVisible;

    // ── CanExecute helpers ────────────────────────────────────────────────────

    private bool IsNotBusy()       => !IsBusy;
    private bool CanBuildSolution() => !IsBusy && HasSolution;
}
