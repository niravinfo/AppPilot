using AppPilot.Domain.Enums;
using AppPilot.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;

namespace AppPilot.ViewModels;

public partial class DiscoveredServiceItemViewModel : ViewModelBase
{
    public DiscoveredService Service { get; }

    [ObservableProperty]
    private bool _isSelected = true;

    [ObservableProperty]
    private bool _isExpanded;

    public string ProjectName => Service.ProjectName;
    public string DisplayName => Service.DisplayName;
    public ServiceType Type => Service.Type;
    public string TypeName => Type.ToString();
    public string ProjectPath => Service.ProjectPath;
    public string CsprojPath => Service.CsprojPath;
    public int? Port => Service.Port;
    public int DisplayOrder => Service.DisplayOrder;
    public string HealthCheckUrl => Service.HealthCheckUrl;
    public string? GrpcEndpoint => Service.GrpcEndpoint;
    public string? SwaggerUrl => Service.SwaggerUrl;
    public bool UseWindowsService => Service.UseWindowsService;
    public string GroupName
    {
        get => string.IsNullOrEmpty(Service.GroupId) ? "Ungrouped" : Service.GroupId;
    }
    public bool HasGroup => !string.IsNullOrEmpty(Service.GroupId);

    public IReadOnlyDictionary<string, string> EnvironmentVariables => Service.EnvironmentVariables;
    public int EnvVarCount => Service.EnvironmentVariables.Count;
    public List<string> Dependencies => Service.Dependencies;

    public bool HasPort => Port.HasValue;
    public bool HasHealthCheckUrl => !string.IsNullOrEmpty(HealthCheckUrl);
    public bool HasGrpcEndpoint => !string.IsNullOrEmpty(GrpcEndpoint);
    public bool HasSwaggerUrl => !string.IsNullOrEmpty(SwaggerUrl);
    public bool HasEnvVars => EnvVarCount > 0;
    public bool HasDependencies => Dependencies.Count > 0;

    public DiscoveredServiceItemViewModel(DiscoveredService service)
    {
        Service = service;
        _isSelected = service.IsSelected;
    }

    partial void OnIsSelectedChanged(bool value)
    {
        Service.IsSelected = value;
    }

    public void NotifyPropertiesChanged()
    {
        OnPropertyChanged(nameof(GroupName));
        OnPropertyChanged(nameof(HasGroup));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(TypeName));
        OnPropertyChanged(nameof(Type));
        OnPropertyChanged(nameof(Port));
        OnPropertyChanged(nameof(HasPort));
        OnPropertyChanged(nameof(HealthCheckUrl));
        OnPropertyChanged(nameof(HasHealthCheckUrl));
        OnPropertyChanged(nameof(GrpcEndpoint));
        OnPropertyChanged(nameof(HasGrpcEndpoint));
        OnPropertyChanged(nameof(SwaggerUrl));
        OnPropertyChanged(nameof(HasSwaggerUrl));
        OnPropertyChanged(nameof(EnvVarCount));
        OnPropertyChanged(nameof(HasEnvVars));
        OnPropertyChanged(nameof(HasDependencies));
    }

    public ManagedServiceConfig ToManagedServiceConfig()
    {
        return new ManagedServiceConfig
        {
            Name = Service.ProjectName,
            DisplayName = Service.DisplayName,
            Type = Service.Type,
            ExecutablePath = Service.ExecutablePath,
            WorkingDirectory = Service.WorkingDirectory,
            CsprojPath = Service.CsprojPath,
            Port = Service.Port,
            HealthCheckUrl = Service.HealthCheckUrl,
            Arguments = Service.Arguments,
            Environment = new Dictionary<string, string>(Service.EnvironmentVariables),
            UseWindowsService = Service.UseWindowsService,
            Dependencies = new List<string>(Service.Dependencies),
            GroupId = Service.GroupId,
            DisplayOrder = Service.DisplayOrder > 0 ? Service.DisplayOrder : null,
        };
    }
}
