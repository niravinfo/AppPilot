using AppPilot.Domain.Enums;
using System;

namespace AppPilot.Models;

public class ServiceInfo
{
    public ManagedServiceConfig Config { get; set; } = new();
    public ServiceStatus Status { get; set; } = ServiceStatus.NotInstalled;
    public int? ProcessId { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime LastChecked { get; set; }
}
