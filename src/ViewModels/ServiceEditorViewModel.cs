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
    [NotifyPropertyChangedFor(nameof(IsWorkerType))]
    [NotifyPropertyChangedFor(nameof(IsApiOrGrpcType))]
    private ServiceType _serviceType = ServiceType.WebApi;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _groupId = string.Empty;

    [ObservableProperty]
    private string _executablePath = string.Empty;

    [ObservableProperty]
    private string _csprojPath = string.Empty;

    [ObservableProperty]
    private string _arguments = string.Empty;

    [ObservableProperty]
    private string _workingDirectory = string.Empty;

    [ObservableProperty]
    private string _portText = string.Empty;

    [ObservableProperty]
    private string _healthCheckUrl = string.Empty;

    [ObservableProperty]
    private bool _useWindowsService;

    [ObservableProperty]
    private int _displayOrder = 999;

    [ObservableProperty]
    private string _dependenciesText = string.Empty;

    [ObservableProperty]
    private string _newGroupName = string.Empty;

    [ObservableProperty]
    private EnvironmentVariableViewModel? _selectedEnvVar;

    public ObservableCollection<GroupConfig> Groups { get; }
    public ObservableCollection<EnvironmentVariableViewModel> EnvironmentVariables { get; } = [];
    public IReadOnlyList<ServiceType> ServiceTypes { get; } = Enum.GetValues<ServiceType>();

    public bool IsNew { get; }
    public string Title => IsNew ? "Add Service" : $"Edit — {DisplayName}";
    public string SaveButtonText => IsNew ? "Add Service" : "Save Changes";
    public bool IsWorkerType => ServiceType == ServiceType.Worker;
    public bool IsApiOrGrpcType => ServiceType == ServiceType.Grpc || ServiceType == ServiceType.WebApi;

    public ServiceEditorViewModel(ObservableCollection<GroupConfig> groups)
    {
        IsNew = true;
        Groups = groups;
    }

    public ServiceEditorViewModel(ManagedServiceConfig config, ObservableCollection<GroupConfig> groups)
    {
        IsNew = false;
        _serviceType = config.Type;
        _displayName = config.DisplayName;
        _name = config.Name;
        _executablePath = config.ExecutablePath;
        _csprojPath = config.CsprojPath;
        _arguments = config.Arguments;
        _workingDirectory = config.WorkingDirectory;
        _portText = config.Port?.ToString() ?? string.Empty;
        _healthCheckUrl = config.HealthCheckUrl;
        _useWindowsService = config.UseWindowsService;
        _displayOrder = config.DisplayOrder ?? 999;
        _dependenciesText = string.Join(", ", config.Dependencies);
        foreach (var (key, value) in config.Environment)
        {
            EnvironmentVariables.Add(new EnvironmentVariableViewModel(key, value));
        }

        Groups = groups;
        _groupId = ResolveGroupId(config.GroupId);
    }

    private string ResolveGroupId(string configGroupId)
    {
        if (string.IsNullOrEmpty(configGroupId))
            return string.Empty;

        if (Groups.Any(g => g.Id == configGroupId))
            return configGroupId;

        var matchByName = Groups.FirstOrDefault(g => g.Name.Equals(configGroupId, StringComparison.OrdinalIgnoreCase));
        if (matchByName != null)
            return matchByName.Id;

        return string.Empty;
    }

    public void ApplyTo(ManagedServiceConfig config)
    {
        config.Name = Name;
        config.DisplayName = DisplayName;
        config.GroupId = GroupId;
        config.Type = ServiceType;
        config.ExecutablePath = ExecutablePath;
        config.CsprojPath = CsprojPath;
        config.Arguments = Arguments;
        config.WorkingDirectory = WorkingDirectory;
        config.Port = IsApiOrGrpcType && int.TryParse(PortText, out var port) ? port : null;
        config.HealthCheckUrl = IsApiOrGrpcType ? HealthCheckUrl : string.Empty;
        config.UseWindowsService = IsWorkerType && UseWindowsService;
        config.DisplayOrder = DisplayOrder;
        config.Dependencies = [.. DependenciesText
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
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
    private void BrowseCsprojPath()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Project File",
            Filter = "Project Files (*.csproj, *.slnx)|*.csproj;*.slnx|All Files (*.*)|*.*",
            CheckFileExists = false
        };
        if (dialog.ShowDialog() == true)
            CsprojPath = dialog.FileName;
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
        var existing = Groups.FirstOrDefault(g => g.Id.Equals(trimmed, StringComparison.OrdinalIgnoreCase) ||
                                                   g.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            GroupId = existing.Id;
            NewGroupName = string.Empty;
            return;
        }

        var maxOrder = Groups.Count > 0 ? Groups.Max(g => g.DisplayOrder) : 0;
        var newGroup = new GroupConfig
        {
            Id = trimmed,
            Name = trimmed,
            DisplayOrder = maxOrder + 1
        };
        Groups.Add(newGroup);
        GroupId = trimmed;
        NewGroupName = string.Empty;
    }
}
