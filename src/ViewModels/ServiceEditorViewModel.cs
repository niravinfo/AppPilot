using AppPilot.Domain.Enums;
using AppPilot.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AppPilot.ViewModels;

public partial class ServiceEditorViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _groupId = string.Empty;

    [ObservableProperty]
    private ServiceType _serviceType = ServiceType.WebApi;

    public ObservableCollection<GroupConfig> Groups { get; }

    [ObservableProperty]
    private string _executablePath = string.Empty;

    [ObservableProperty]
    private string _arguments = string.Empty;

    [ObservableProperty]
    private string _workingDirectory = string.Empty;

    [ObservableProperty]
    private string _portText = string.Empty;

    [ObservableProperty]
    private string _healthCheckUrl = string.Empty;


    [ObservableProperty]
    private int _displayOrder = 999;

    [ObservableProperty]
    private string _newGroupName = string.Empty;

    [ObservableProperty]
    private string _dependenciesText = string.Empty;

    [ObservableProperty]
    private EnvironmentVariableViewModel? _selectedEnvVar;

    public ObservableCollection<EnvironmentVariableViewModel> EnvironmentVariables { get; } = [];
    public IReadOnlyList<ServiceType> ServiceTypes { get; } = System.Enum.GetValues<ServiceType>();

    public bool IsNew { get; }
    public string Title => IsNew ? "Add Service" : $"Edit — {DisplayName}";
    public string SaveButtonText => IsNew ? "Add Service" : "Save Changes";

    public ServiceEditorViewModel(ObservableCollection<GroupConfig> groups)
    {
        IsNew = true;
        Groups = groups;
    }

    public ServiceEditorViewModel(ManagedServiceConfig config, ObservableCollection<GroupConfig> groups)
    {
        IsNew = false;
        _displayName = config.DisplayName;
        _name = config.Name;
        _groupId = config.GroupId;
        _serviceType = config.Type;
        _executablePath = config.ExecutablePath;
        _arguments = config.Arguments;
        _workingDirectory = config.WorkingDirectory;
        _portText = config.Port?.ToString() ?? string.Empty;
        _healthCheckUrl = config.HealthCheckUrl;
        _displayOrder = config.DisplayOrder ?? 999;
        _dependenciesText = string.Join(", ", config.Dependencies);
        foreach (var (key, value) in config.Environment)
        {
            EnvironmentVariables.Add(new EnvironmentVariableViewModel(key, value));
        }

        Groups = groups;
    }

    public void ApplyTo(ManagedServiceConfig config)
    {
        config.Name = Name;
        config.DisplayName = DisplayName;
        config.GroupId = GroupId;
        config.Type = ServiceType;
        config.ExecutablePath = ExecutablePath;
        config.Arguments = Arguments;
        config.WorkingDirectory = WorkingDirectory;
        config.Port = int.TryParse(PortText, out var port) ? port : null;
        config.HealthCheckUrl = HealthCheckUrl;
        config.DisplayOrder = DisplayOrder;
        config.Dependencies = [.. DependenciesText
            .Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries)];
        config.Environment = EnvironmentVariables.ToDictionary(e => e.Key, e => e.Value);
    }

    public ManagedServiceConfig ToConfig()
    {
        var config = new ManagedServiceConfig();
        ApplyTo(config);
        return config;
    }

    [RelayCommand]
    private void AddEnvironmentVariable()
    {
        var item = new EnvironmentVariableViewModel();
        EnvironmentVariables.Add(item);
        SelectedEnvVar = item;
    }

    [RelayCommand]
    private void RemoveEnvironmentVariable(EnvironmentVariableViewModel? item)
    {
        if (item is not null)
            EnvironmentVariables.Remove(item);
    }

    [RelayCommand]
    private void BrowseExecutablePath()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Executable",
            Filter = "Executables (*.exe)|*.exe|All Files (*.*)|*.*",
            CheckFileExists = false
        };
        if (dialog.ShowDialog() == true)
            ExecutablePath = dialog.FileName;
    }

    [RelayCommand]
    private void BrowseWorkingDirectory()
    {
        var dialog = new OpenFolderDialog { Title = "Select Working Directory" };
        if (dialog.ShowDialog() == true)
            WorkingDirectory = dialog.FolderName;
    }

    [RelayCommand]
    private void AddNewGroup()
    {
        if (string.IsNullOrWhiteSpace(NewGroupName))
            return;

        var trimmed = NewGroupName.Trim();
        if (Groups.Any(g => g.Name.Equals(trimmed, System.StringComparison.OrdinalIgnoreCase)))
            return;

        var newGroup = new GroupConfig
        {
            Id = Guid.NewGuid().ToString(),
            Name = trimmed
        };
        Groups.Add(newGroup);
        GroupId = newGroup.Id;
        NewGroupName = string.Empty;
    }
}
