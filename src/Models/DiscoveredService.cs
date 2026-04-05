using AppPilot.Domain.Enums;
using System.Collections.Generic;

namespace AppPilot.Models;

public class DiscoveredService
{
    public string ProjectPath { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public ServiceType Type { get; set; }
    public string ExecutablePath { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public string CsprojPath { get; set; } = string.Empty;
    public int? Port { get; set; }
    public string HealthCheckUrl { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
    public bool UseWindowsService { get; set; } = false;
    public string? GrpcEndpoint { get; set; }
    public string? SwaggerUrl { get; set; }
    public string? OpenApiPath { get; set; }
    public bool IsSelected { get; set; } = true;
    public int DisplayOrder { get; set; }
    public string GroupId { get; set; } = string.Empty;
}
