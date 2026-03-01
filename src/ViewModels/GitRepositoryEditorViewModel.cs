using AppPilot.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Linq;

namespace AppPilot.ViewModels;

public partial class GitRepositoryEditorViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _localPath = string.Empty;

    [ObservableProperty]
    private string _solutionPath = string.Empty;

    [ObservableProperty]
    private string _defaultBranch = "main";

    [ObservableProperty]
    private string _linkedServiceNamesText = string.Empty;

    public bool IsNew { get; }
    public string Title => IsNew ? "Add Git Repository" : $"Edit — {DisplayName}";
    public string SaveButtonText => IsNew ? "Add Repository" : "Save Changes";

    public GitRepositoryEditorViewModel()
    {
        IsNew = true;
    }

    public GitRepositoryEditorViewModel(GitRepositoryConfig config)
    {
        IsNew = false;
        _displayName = config.DisplayName;
        _name = config.Name;
        _localPath = config.LocalPath;
        _solutionPath = config.SolutionPath;
        _defaultBranch = config.DefaultBranch;
        _linkedServiceNamesText = string.Join(", ", config.LinkedServiceNames);
    }

    public void ApplyTo(GitRepositoryConfig config)
    {
        config.Name = Name;
        config.DisplayName = DisplayName;
        config.LocalPath = LocalPath;
        config.SolutionPath = SolutionPath;
        config.DefaultBranch = DefaultBranch;
        config.LinkedServiceNames = [
            .. LinkedServiceNamesText
                .Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries)
        ];
    }

    public GitRepositoryConfig ToConfig()
    {
        var config = new GitRepositoryConfig();
        ApplyTo(config);
        return config;
    }

    [RelayCommand]
    private void BrowseLocalPath()
    {
        var dialog = new OpenFolderDialog { Title = "Select Repository Folder" };
        if (dialog.ShowDialog() == true)
            LocalPath = dialog.FolderName;
    }

    [RelayCommand]
    private void BrowseSolutionPath()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Solution File",
            Filter = "Solution Files (*.sln;*.slnx)|*.sln;*.slnx|All Files (*.*)|*.*"
        };
        if (dialog.ShowDialog() == true)
            SolutionPath = dialog.FileName;
    }
}
