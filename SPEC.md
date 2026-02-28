# AppPilot - Specification Document

## 1. Project Overview

### Project Name
**AppPilot**

### Project Type
Windows Desktop Application (WPF)

### Core Feature Summary
A lightweight Windows desktop application for managing multiple .NET worker services, gRPC APIs, and Web APIs locally during development. Provides a unified UI to install, start, stop, delete, and monitor the status of multiple .NET projects without consuming excessive memory.

### Target Users
- .NET developers working in microservices environments
- Development teams needing to run multiple dependent services locally
- Developers with limited RAM who need lightweight alternatives to Docker

---

## 2. User Stories

| ID | Story | Priority |
|----|-------|----------|
| US001 | As a developer, I want to configure all my services in a JSON file so I can easily share the configuration with team members | High |
| US002 | As a developer, I want to see all services in a grid with their current status so I can quickly assess which services are running | High |
| US003 | As a developer, I want to start/stop individual services with a single click so I don't have to use command line | High |
| US004 | As a developer, I want to install a worker service to Windows Service Control Manager so it runs as a Windows service | High |
| US005 | As a developer, I want to uninstall a Windows service so I can cleanly remove it | High |
| US006 | As a developer, I want to see real-time status updates so I know immediately when a service changes state | High |
| US007 | As a developer, I want to start all services in dependency order so I don't have to start them manually | Medium |
| US008 | As a developer, I want to stop all running services at once so I can clean up resources | Medium |
| US009 | As a developer, I want to view the output logs of a service so I can debug issues | Medium |
| US010 | As a developer, I want to run gRPC and Web API projects (not just Windows Services) so I can manage all my dev projects | High |
| US011 | As a developer, I want to auto-start configured services when AppPilot launches so I don't have to start them manually each time | Low |
| US012 | As a developer, I want to minimize AppPilot to system tray so it doesn't clutter my taskbar | Low |

---

## 3. Functional Requirements

### 3.1 Configuration Management

#### FR-001: Load Configuration
The application shall load service configurations from `appsettings.json` on startup.

#### FR-002: Configuration Schema
The configuration shall support the following service types:
- `Worker` - Windows Service using `Microsoft.Extensions.Hosting.WindowsServices`
- `Grpc` - gRPC API running on Kestrel
- `WebApi` - ASP.NET Core Web API running on Kestrel

#### FR-003: Configuration Location
The configuration file shall be located in the application directory by default, with an option to specify a custom path.

### 3.2 Service Discovery & Display

#### FR-004: Service List Display
The application shall display all configured services in a DataGrid with the following columns:
- Name (DisplayName)
- Type (Worker/Grpc/WebApi)
- Status (Running/Stopped/Starting/Stopping/Error/NotInstalled)
- Port (for Grpc/WebApi)
- Actions

#### FR-005: Status Detection - Windows Services
For Worker services, status shall be determined by querying the Windows Service Control Manager via `ServiceController`.

#### FR-006: Status Detection - HTTP Services
For Grpc and WebApi services, status shall be determined by:
1. Checking if the process is running via `Process.GetProcessesByName()`
2. Sending an HTTP HEAD request to the configured endpoint
3. Marking as "Stopped" if the process is not found or endpoint returns non-2xx

### 3.3 Service Control Operations

#### FR-007: Install Windows Service
The application shall install a Worker service as a Windows service using `sc.exe` or `ServiceController`.

**Requirements:**
- Requires administrative privileges
- Service must be stopped before installation
- Display success/failure notification

#### FR-008: Uninstall Windows Service
The application shall uninstall a Windows service using `sc.exe delete`.

**Requirements:**
- Requires administrative privileges
- Service must be stopped before deletion
- Display success/failure notification

#### FR-009: Start Service
The application shall start a service:
- **Worker (installed):** Use `ServiceController.Start()`
- **Worker (not installed) / Grpc / WebApi:** Launch process with configured arguments

#### FR-010: Stop Service
The application shall stop a service:
- **Worker (installed):** Use `ServiceController.Stop()`
- **Worker (not installed) / Grpc / WebApi:** Kill the process gracefully via `Process.Kill()`

#### FR-011: Restart Service
The application shall restart a service by stopping it (if running) then starting it.

### 3.4 Batch Operations

#### FR-012: Start All Services
The application shall start all configured services in dependency order (lowest StartOrder first).

#### FR-013: Stop All Services
The application shall stop all running services in reverse dependency order.

### 3.5 Logging & Monitoring

#### FR-014: Status Polling
The application shall poll service status every 3 seconds (configurable).

#### FR-015: Log Display
The application shall provide a panel to display service output logs.

### 3.6 System Integration

#### FR-016: System Tray
The application shall minimize to system tray when minimized, with context menu:
- Show Window
- Start All
- Stop All
- Exit

---

## 4. Non-Functional Requirements

### 4.1 Performance

| Metric | Target |
|--------|--------|
| Application startup time | < 2 seconds |
| Memory consumption (idle) | < 100 MB |
| Status polling overhead | < 1% CPU |
| UI responsiveness | No freezing during operations |

### 4.2 Compatibility

| Requirement | Specification |
|-------------|---------------|
| .NET Version | .NET 8.0 or higher |
| Target Framework | net8.0-windows |
| Architecture | x64 |
| OS | Windows 10/11 |

### 4.3 Security

- Application requires administrator privileges for Windows Service operations
- No sensitive data shall be logged
- Configuration file paths are validated before use

---

## 5. UI/UX Specification

### 5.1 Window Layout

```
┌─────────────────────────────────────────────────────────────────────┐
│ [Icon] AppPilot                              [_] [□] [X]          │
├─────────────────────────────────────────────────────────────────────┤
│ [Refresh] [Start All] [Stop All] [Settings]                        │
├─────────────────────────────────────────────────────────────────────┤
│ ┌─────────────────────────────────────────────────────────────────┐ │
│ │ Name          │ Type   │ Status     │ Port │ Actions           │ │
│ ├───────────────┼────────┼─────────────┼──────┼───────────────────┤ │
│ │ AuthService   │ Worker │ ● Running  │ -    │ [■][↻][✕][📋]     │ │
│ │ GrpcGateway   │  gRPC  │ ● Running  │ 5002 │ [■][↻][✕][📋]     │ │
│ │ WebApi        │  Web   │ ○ Stopped  │ 5000 │ [▶][  ][✕][📋]    │ │
│ │ PaymentSvc    │ Worker │ ⚠ Error    │ -    │ [▶][  ][✕][📋]    │ │
│ └─────────────────────────────────────────────────────────────────┘ │
├─────────────────────────────────────────────────────────────────────┤
│ Status: 3 services running │ Last update: 12:34:56                │
└─────────────────────────────────────────────────────────────────────┘
```

### 5.2 Color Palette

| Element | Color | Hex Code |
|---------|-------|----------|
| Primary Background | Dark Charcoal | #1E1E1E |
| Secondary Background | Darker Gray | #252526 |
| Accent | Blue | #0078D4 |
| Text Primary | White | #FFFFFF |
| Text Secondary | Light Gray | #CCCCCC |
| Status: Running | Green | #4CAF50 |
| Status: Stopped | Gray | #808080 |
| Status: Error | Red | #F44336 |
| Status: Starting/Stopping | Orange | #FF9800 |
| Button Hover | Lighter Blue | #1E90FF |
| Border | Dark Gray | #3C3C3C |

### 5.3 Typography

| Element | Font | Size | Weight |
|---------|------|------|--------|
| Window Title | Segoe UI | 14px | SemiBold |
| Headers | Segoe UI | 13px | SemiBold |
| Body Text | Segoe UI | 12px | Regular |
| Status Text | Segoe UI | 11px | Regular |
| Buttons | Segoe UI | 12px | Regular |

### 5.4 Component States

#### Buttons
- **Default:** Background #0078D4, Text White
- **Hover:** Background #1E90FF
- **Pressed:** Background #005A9E
- **Disabled:** Background #3C3C3C, Text #808080

#### DataGrid Rows
- **Default:** Background Transparent
- **Hover:** Background #2A2D2E
- **Selected:** Background #094771

### 5.5 Icons & Symbols

| Symbol | Meaning |
|--------|---------|
| ● | Running (filled circle) |
| ○ | Stopped (empty circle) |
| ⚠ | Error/Unknown |
| ▶ | Start |
| ■ | Stop |
| ↻ | Restart |
| ✕ | Delete/Uninstall |
| 📋 | Install (clipboard-like) |
| ⚙ | Settings |

---

## 6. Technical Architecture

### 6.1 Project Structure

```
AppPilot/
├── App.xaml
├── App.xaml.cs
├── AppPilot.csproj
├── appsettings.json
├── README.md
├── Domain/
│   └── Enums/
│       ├── ServiceStatus.cs
│       └── ServiceType.cs
├── Models/
│   ├── ManagedServiceConfig.cs
│   └── ServiceInfo.cs
├── Services/
│   ├── Configuration/
│   │   └── ConfigurationService.cs
│   ├── ServiceControl/
│   │   ├── IServiceController.cs
│   │   ├── WindowsServiceController.cs
│   │   └── ProcessServiceController.cs
│   ├── HealthCheck/
│   │   └── HttpHealthChecker.cs
│   └── Logging/
│       └── LoggingService.cs
├── ViewModels/
│   ├── ViewModelBase.cs
│   ├── RelayCommand.cs
│   ├── MainViewModel.cs
│   └── ServiceItemViewModel.cs
└── Views/
    ├── MainWindow.xaml
    └── MainWindow.xaml.cs
```

### 6.2 Class Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                           ViewModels                                 │
├─────────────────────────────────────────────────────────────────────┤
│ MainViewModel                                                        │
│ ─────────────────────────────────────────────────────────────────  │
│ + Services: ObservableCollection<ServiceItemViewModel>             │
│ + RefreshCommand: ICommand                                          │
│ + StartAllCommand: ICommand                                         │
│ + StopAllCommand: ICommand                                          │
│ + StartServiceCommand: ICommand                                     │
│ + StopServiceCommand: ICommand                                      │
│ + InstallServiceCommand: ICommand                                   │
│ + UninstallServiceCommand: ICommand                                 │
│ + RestartServiceCommand: ICommand                                   │
│ ─────────────────────────────────────────────────────────────────  │
│ + LoadConfiguration(): void                                         │
│ + RefreshStatus(): void                                              │
│ + StartAll(): Task                                                  │
│ + StopAll(): Task                                                   │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                                    │ uses
                                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│                            Models                                    │
├─────────────────────────────────────────────────────────────────────┤
│ ManagedServiceConfig              ServiceInfo                       │
│ ───────────────────────            ───────────                       │
│ + Name: string                     + Config: ManagedServiceConfig    │
│ + DisplayName: string              + Status: ServiceStatus           │
│ + Type: ServiceType                + ProcessId: int?                 │
│ + ExecutablePath: string           + Port: int?                      │
│ + Arguments: string                + ErrorMessage: string           │
│ + WorkingDirectory: string         + LastChecked: DateTime          │
│ + AutoStart: bool                                                     │
│ + StartOrder: int                                                      │
│ + Dependencies: List<string>                                         │
│ + Port: int?                                                          │
│ + HealthCheckUrl: string                                              │
│ + EnvironmentVariables: Dictionary<string,string>                   │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                                    │ uses
                                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│                          Services                                    │
├─────────────────────────────────────────────────────────────────────┤
│ ConfigurationService              WindowsServiceController          │
│ ───────────────────────            ──────────────────────────────  │
│ + Load(): AppSettings               + Install(service): bool        │
│ + Save(config): void                + Uninstall(service): bool      │
│ + GetServices(): List<Config>        + Start(service): bool          │
│                                       + Stop(service): bool           │
│                                       + GetStatus(service): Status    │
├─────────────────────────────────────────────────────────────────────┤
│ ProcessServiceController           HttpHealthChecker                │
│ ───────────────────────            ────────────────────────        │
│ + Start(config): Process            + CheckHealth(url): Task<bool>  │
│ + Stop(process): void                                                 │
│ + GetRunningProcess(name): Process                                    │
└─────────────────────────────────────────────────────────────────────┘
```

### 6.3 Data Flow

```
┌─────────────┐     ┌─────────────────┐     ┌──────────────────┐
│  appsettings│────▶│ Configuration   │────▶│ MainViewModel   │
│    .json    │     │   Service       │     │                  │
└─────────────┘     └─────────────────┘     └────────┬─────────┘
                                                      │
                                                      │ binds
                                                      ▼
┌─────────────┐     ┌─────────────────┐     ┌──────────────────┐
│   Windows   │◀────│ Service         │◀────│ DataGrid         │
│   Services  │     │ Controllers     │     │ (UI)             │
└─────────────┘     └─────────────────┘     └──────────────────┘
                                                      │
                                                      │ polls every 3s
                                                      ▼
┌─────────────┐     ┌─────────────────┐     ┌──────────────────┐
│  Processes  │◀────│ Process         │◀────│ ServiceStatus    │
│             │     │ Controller      │     │ Checker          │
└─────────────┘     └─────────────────┘     └──────────────────┘
```

---

## 7. Configuration Schema

### 7.1 appsettings.json

```json
{
  "AppPilot": {
    "ConfigurationPath": "",
    "PollingIntervalMs": 3000,
    "AutoStartServices": false,
    "MinimizeToTray": true,
    "LogDirectory": "Logs"
  },
  "Services": [
    {
      "Name": "AuthService",
      "DisplayName": "Auth Service",
      "Type": "Worker",
      "ExecutablePath": "C:\\Projects\\AuthService\\bin\\Debug\\net8.0\\AuthService.exe",
      "Arguments": "",
      "WorkingDirectory": "C:\\Projects\\AuthService\\bin\\Debug\\net8.0",
      "AutoStart": true,
      "StartOrder": 1,
      "Dependencies": [],
      "Environment": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    },
    {
      "Name": "GrpcGateway",
      "DisplayName": "gRPC Gateway",
      "Type": "Grpc",
      "ExecutablePath": "C:\\Projects\\GrpcGateway\\bin\\Debug\\net8.0\\GrpcGateway.exe",
      "Arguments": "--urls=http://localhost:5002",
      "WorkingDirectory": "C:\\Projects\\GrpcGateway\\bin\\Debug\\net8.0",
      "Port": 5002,
      "HealthCheckUrl": "http://localhost:5002/health",
      "AutoStart": true,
      "StartOrder": 2,
      "Dependencies": ["AuthService"],
      "Environment": {}
    },
    {
      "Name": "WebApi",
      "DisplayName": "Web API",
      "Type": "WebApi",
      "ExecutablePath": "C:\\Projects\\WebApi\\bin\\Debug\\net8.0\\WebApi.exe",
      "Arguments": "--urls=http://localhost:5000",
      "WorkingDirectory": "C:\\Projects\\WebApi\\bin\\Debug\\net8.0",
      "Port": 5000,
      "HealthCheckUrl": "http://localhost:5000/health",
      "AutoStart": false,
      "StartOrder": 3,
      "Dependencies": ["GrpcGateway"],
      "Environment": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  ]
}
```

### 7.2 Schema Definition

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| Name | string | Yes | Unique identifier for the service |
| DisplayName | string | Yes | Friendly name for display in UI |
| Type | enum | Yes | Worker, Grpc, or WebApi |
| ExecutablePath | string | Yes | Full path to the executable |
| Arguments | string | No | Command line arguments |
| WorkingDirectory | string | No | Working directory for the process |
| Port | int | No | Port number (required for Grpc/WebApi) |
| HealthCheckUrl | string | No | URL for HTTP health check |
| AutoStart | bool | No | Whether to start automatically |
| StartOrder | int | No | Order for batch start (default: 0) |
| Dependencies | string[] | No | List of service names this depends on |
| Environment | object | No | Environment variables |

---

## 8. API/Interface Definitions

### 8.1 Enums

```csharp
public enum ServiceType
{
    Worker,   // Windows Service
    Grpc,     // gRPC API running on Kestrel
    WebApi    // ASP.NET Core Web API
}

public enum ServiceStatus
{
    NotInstalled,  // Windows service not installed
    Stopped,       // Service is not running
    Starting,      // Service is starting
    Running,       // Service is running
    Stopping,      // Service is stopping
    Error          // Service in error state
}
```

### 8.2 Configuration Model

```csharp
public class AppSettings
{
    public AppPilotSettings AppPilot { get; set; }
    public List<ManagedServiceConfig> Services { get; set; }
}

public class AppPilotSettings
{
    public string ConfigurationPath { get; set; }
    public int PollingIntervalMs { get; set; } = 3000;
    public bool AutoStartServices { get; set; }
    public bool MinimizeToTray { get; set; } = true;
    public string LogDirectory { get; set; } = "Logs";
}

public class ManagedServiceConfig
{
    public string Name { get; set; }
    public string DisplayName { get; set; }
    public ServiceType Type { get; set; }
    public string ExecutablePath { get; set; }
    public string Arguments { get; set; }
    public string WorkingDirectory { get; set; }
    public int? Port { get; set; }
    public string HealthCheckUrl { get; set; }
    public bool AutoStart { get; set; }
    public int StartOrder { get; set; }
    public List<string> Dependencies { get; set; }
    public Dictionary<string, string> Environment { get; set; }
}

public class ServiceInfo
{
    public ManagedServiceConfig Config { get; set; }
    public ServiceStatus Status { get; set; }
    public int? ProcessId { get; set; }
    public string ErrorMessage { get; set; }
    public DateTime LastChecked { get; set; }
}
```

---

## 9. Edge Cases & Error Handling

### 9.1 Error Scenarios

| Scenario | Handling |
|----------|----------|
| Executable path not found | Show error icon, disable start, display "File not found" message |
| Port already in use | Process starts but health check fails, show error status |
| Service fails to start | Capture error message, show error status with message |
| Health check timeout | Mark as "Unknown" after 5 seconds |
| Admin privileges required | Show UAC prompt or display message to run as admin |
| Service dependency missing | Show warning, prevent start until dependency is running |
| Config file missing | Create default config with example services |
| Config file invalid | Show error dialog, load with empty service list |
| Process crashes | Detect via polling, update status to "Error" |
| Rapid start/stop clicks | Disable buttons during operation, show loading state |

### 9.2 Logging

- Log all service operations (start, stop, install, uninstall)
- Log errors with stack traces
- Log to file in `Logs/AppPilot_{date}.log`
- Retain logs for 7 days

---

## 10. Future Considerations

### 10.1 Potential Features (Out of Scope for v1)

| Feature | Description |
|---------|-------------|
| Service Templates | Pre-defined templates for common service types |
| Remote Management | Manage services on remote machines |
| Web Dashboard | Web-based UI alternative |
| Configuration Editor | UI to edit services instead of JSON |
| Service Groups | Group services by project/team |
| Notifications | Windows toast notifications on status change |
| Export/Import | Export/import configuration |
| Dark/Light Theme | Theme toggle |

### 10.2 Known Limitations

- Does not support 32-bit executables (x64 only)
- Requires Windows 10/11 (not tested on Windows Server)
- No support for .NET Framework projects (only .NET 6+)
- Health check assumes Kestrel-based services

---

## 11. Acceptance Criteria

### 11.1 Core Functionality

- [ ] Application launches and displays configured services
- [ ] Services can be started and stopped individually
- [ ] Windows services can be installed and uninstalled
- [ ] Status updates automatically every 3 seconds
- [ ] Start All / Stop All works in dependency order

### 11.2 Service Types

- [ ] Worker services (Windows Service) are supported
- [ ] gRPC services are supported
- [ ] Web API services are supported

### 11.3 UI/UX

- [ ] Status is displayed with appropriate colors and icons
- [ ] Actions are accessible via buttons
- [ ] System tray icon appears when minimized

### 11.4 Performance

- [ ] Application uses less than 100 MB RAM when idle
- [ ] Status polling does not freeze UI
- [ ] Service operations complete within reasonable time

---

## 12. Appendix

### 12.1 Dependencies

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.2" />
    <PackageReference Include="Microsoft.Extensions.Configuration" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
    <PackageReference Include="Serilog" Version="3.1.1" />
    <PackageReference Include="Serilog.Sinks.File" Version="5.0.0" />
  </ItemGroup>
</Project>
```

### 12.2 Minimum System Requirements

| Requirement | Specification |
|-------------|---------------|
| OS | Windows 10 version 1809 or later |
| RAM | 4 GB (application uses ~100 MB) |
| Disk | 50 MB for application |
| .NET | .NET 8.0 Runtime |
| Privileges | Admin rights for Windows Service operations |

---

*Document Version: 1.0*
*Last Updated: February 2026*
