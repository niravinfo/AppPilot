# AppPilot - Specification Document

## 1. Project Overview

### Project Name
**AppPilot**

### Project Type
Windows Desktop Application (WPF)

### Core Feature Summary
A lightweight Windows desktop application for managing multiple .NET worker services, gRPC APIs, and Web APIs locally during development. Provides automatic service discovery, group management, individual service editing, build integration, and a unified UI to install, start, stop, and monitor the status of multiple .NET projects without consuming excessive memory.

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
| US013 | As a developer, I want to automatically discover .NET services from a root directory so I don't have to manually configure each one | High |
| US014 | As a developer, I want to selectively import discovered services and edit their details before importing | High |
| US015 | As a developer, I want to organize services into custom groups with names and colors for better visual organization | High |
| US016 | As a developer, I want to assign groups to multiple discovered services at once | Medium |
| US017 | As a developer, I want to build any service directly from the UI using its .csproj file | Medium |
| US018 | As a developer, I want to link git repositories to services for quick access to source code | Low |
| US019 | As a developer, I want to toggle between dark and light themes | Medium |

---

## 3. Functional Requirements

### 3.1 Configuration Management

#### FR-001: Load Configuration
The application shall load service configurations from `AppData.json` on startup.

#### FR-002: Configuration Schema
The configuration shall support the following service types:
- `Worker` - Windows Service using `Microsoft.Extensions.Hosting.WindowsServices`
- `Grpc` - gRPC API running on Kestrel
- `WebApi` - ASP.NET Core Web API running on Kestrel

#### FR-003: Configuration Location
The configuration file shall be located in the application directory by default, with an option to specify a custom path via `BasePath`.

#### FR-004: Save Configuration
The application shall persist all changes (services, groups, repositories) to `AppData.json` automatically after any modification.

### 3.2 Service Discovery & Import

#### FR-005: Automatic Service Discovery
The application shall scan a user-selected directory (up to 2 folder levels deep) for `.csproj` files that have a `Properties/launchSettings.json`.

#### FR-006: Service Type Detection
Discovered services shall be classified as:
- **Worker** — detected by `Microsoft.NET.Sdk.Worker` or `OutputType=Exe` without ASP.NET Core references
- **gRPC** — detected by scanning `Program.cs` for `MapGrpcService` or `AddGrpc`
- **WebApi** — all other ASP.NET Core projects

#### FR-007: Port & Endpoint Extraction
The application shall extract HTTPS ports from non-IIS profiles in `launchSettings.json` and auto-generate `--urls` arguments for API/gRPC services.

#### FR-008: Selective Import
Users shall be able to select/deselect individual discovered services and import only the selected ones. Duplicate names shall receive automatic suffixes (`_1`, `_2`, etc.).

#### FR-009: Edit Before Import
Users shall be able to open the full service editor for any discovered service to modify its details (name, group, type, paths, environment variables, etc.) before importing.

#### FR-010: Bulk Group Assignment
Users shall be able to assign an existing group (or create a new one) to all selected discovered services at once.

### 3.3 Service Display & Grouping

#### FR-011: Service List Display
The application shall display all configured services in a card-based layout with the following information:
- Name (DisplayName)
- Type icon (Worker/gRPC/WebApi)
- Status (Running/Stopped/Starting/Stopping/Error/NotInstalled)
- Port (for Grpc/WebApi)
- Group badge
- Actions (Start, Stop, Restart, Build, Edit, Delete, Install/Uninstall)

#### FR-012: Group Filtering
Services shall be filterable by group. An "Ungrouped" filter shall show services without a group assignment.

#### FR-013: Group Management
Users shall be able to create, edit, and delete groups with the following properties:
- **Name** — unique identifier (also used as the group ID)
- **Color** — hex color code for visual identification
- **DisplayOrder** — controls sort order in the UI

### 3.4 Service Control Operations

#### FR-014: Install Windows Service
The application shall install a Worker service as a Windows service using `sc.exe`.

**Requirements:**
- Requires administrative privileges
- Service must be stopped before installation
- Display success/failure notification

#### FR-015: Uninstall Windows Service
The application shall uninstall a Windows service using `sc.exe delete`.

**Requirements:**
- Requires administrative privileges
- Service must be stopped before deletion
- Display success/failure notification

#### FR-016: Start Service
The application shall start a service:
- **Worker (installed):** Use `ServiceController.Start()`
- **Worker (not installed) / Grpc / WebApi:** Launch process with configured arguments

#### FR-017: Stop Service
The application shall stop a service:
- **Worker (installed):** Use `ServiceController.Stop()`
- **Worker (not installed) / Grpc / WebApi:** Kill the process gracefully

#### FR-018: Restart Service
The application shall restart a service by stopping it (if running) then starting it.

### 3.5 Service Editor

#### FR-019: Service Editing
Users shall be able to edit service properties through a dedicated dialog:
- Display Name, Name
- Group assignment (with inline new group creation)
- Service Type
- Executable Path (with file browser)
- Arguments
- Working Directory (with folder browser)
- Port
- Health Check URL
- Display Order
- Dependencies (comma-separated)
- Environment Variables (add/remove key-value pairs)

### 3.6 Build Integration

#### FR-020: Service Build
The application shall build any service with a configured `CsprojPath` using `dotnet build`. Build output shall be displayed in a console panel within the UI.

### 3.7 Batch Operations

#### FR-021: Start All Services
The application shall start all configured services in dependency order (lowest DisplayOrder first).

#### FR-022: Stop All Services
The application shall stop all running services in reverse dependency order.

### 3.8 Logging & Monitoring

#### FR-023: Status Polling
The application shall poll service status every 3 seconds (configurable).

#### FR-024: Log Display
The application shall provide a panel to display service build and output logs.

### 3.9 Git Repository Integration

#### FR-025: Repository Management
Users shall be able to add, edit, and remove git repository links with name, path, and URL. Repositories can be opened in the default browser or file explorer.

### 3.10 Theme Support

#### FR-026: Dark/Light Theme
The application shall support both dark and light themes, toggleable from the toolbar. Theme preference shall be persisted.

### 3.11 System Integration

#### FR-027: System Tray
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
| .NET Version | .NET 10 |
| Target Framework | net10.0-windows |
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
┌─────────────────────────────────────────────────────────────────────────────────┐
│ [Icon] AppPilot                                              [_] [□] [X]      │
├─────────────────────────────────────────────────────────────────────────────────┤
│ [▶ Start All] [■ Stop All] [⟳ Refresh] [⚙ Settings] [🔍 Discover] [📁 Groups] │
│                                                  [🌙/☀ Theme] [📂 Open Folder] │
├─────────────────────────────────────────────────────────────────────────────────┤
│ [All] [Workers] [gRPC] [Web APIs]  │  [Group Filter: All ▼]                    │
├─────────────────────────────────────────────────────────────────────────────────┤
│ ┌─────────────────────────────────────────────────────────────────────────────┐ │
│ │ ┌─────────────────────────────────────────────────────────────────────────┐ │ │
│ │ │ [Icon] AuthService           ● Running    [Workers]    [▶][■][↻][🔨][✏]│ │ │
│ │ │ C:\Projects\AuthService\bin\Debug\net10.0                               │ │ │
│ │ └─────────────────────────────────────────────────────────────────────────┘ │ │
│ │ ┌─────────────────────────────────────────────────────────────────────────┐ │ │
│ │ │ [Icon] GrpcGateway             ● Running    [APIs]      [▶][■][↻][🔨][✏]│ │ │
│ │ │ C:\Projects\GrpcGateway\bin\Debug\net10.0                               │ │ │
│ │ └─────────────────────────────────────────────────────────────────────────┘ │ │
│ │ ┌─────────────────────────────────────────────────────────────────────────┐ │ │
│ │ │ [Icon] WebApi                  ○ Stopped    [Ungrouped] [▶][  ][  ][🔨][✏]│ │ │
│ │ │ C:\Projects\WebApi\bin\Debug\net10.0                                    │ │ │
│ │ └─────────────────────────────────────────────────────────────────────────┘ │ │
│ └─────────────────────────────────────────────────────────────────────────────┘ │
├─────────────────────────────────────────────────────────────────────────────────┤
│ [Output Console]  [Clear]                                                      │
│ $ dotnet build AuthService.csproj                                              │
│ Build succeeded. 0 Warning(s) 0 Error(s)                                       │
└─────────────────────────────────────────────────────────────────────────────────┘
```

### 5.2 Color Palette

#### Light Theme
| Element | Color | Hex Code |
|---------|-------|----------|
| Primary Background | Light Gray | #F1F5F9 |
| Card Background | White | #FFFFFF |
| Accent | Indigo | #6366F1 |
| Text Primary | Dark Slate | #1E293B |
| Text Secondary | Slate | #64748B |
| Text Muted | Light Slate | #94A3B8 |
| Status: Running | Green | #16A34A |
| Status: Stopped | Gray | #94A3B8 |
| Status: Error | Red | #DC2626 |
| Border | Light Gray | #E2E8F0 |

#### Dark Theme
| Element | Color | Hex Code |
|---------|-------|----------|
| Primary Background | Dark Slate | #0F172A |
| Card Background | Darker Slate | #1E293B |
| Accent | Indigo | #6366F1 |
| Text Primary | Light Gray | #E2E8F0 |
| Text Secondary | Slate | #94A3B8 |
| Text Muted | Dark Slate | #64748B |
| Status: Running | Green | #22C55E |
| Status: Stopped | Gray | #64748B |
| Status: Error | Red | #EF4444 |
| Border | Dark Gray | #334155 |

### 5.3 Typography

| Element | Font | Size | Weight |
|---------|------|------|--------|
| Window Title | Segoe UI | 14px | SemiBold |
| Headers | Segoe UI | 13px | SemiBold |
| Body Text | Segoe UI | 12px | Regular |
| Status Text | Segoe UI | 11px | Regular |
| Buttons | Segoe UI | 12px | Regular |
| Mono/Paths | Cascadia Code | 11px | Regular |

### 5.4 Component States

#### Buttons
- **Default:** Background from theme, Text from theme
- **Hover:** Lighter background variant
- **Pressed:** Darker background variant
- **Disabled:** Opacity 0.4

#### Cards
- **Default:** Card background, subtle border
- **Hover:** Slightly lighter/darker background
- **Selected:** Accent border

---

## 6. Technical Architecture

### 6.1 Project Structure

```
AppPilot/
├── src/
│   ├── App.xaml / App.xaml.cs
│   ├── AppPilot.csproj
│   ├── Domain/
│   │   └── Enums/
│   │       ├── ServiceStatus.cs
│   │       └── ServiceType.cs
│   ├── Models/
│   │   ├── AppData.cs
│   │   ├── AppPilotSettings.cs
│   │   ├── DiscoveredService.cs
│   │   ├── GitRepositoryConfig.cs
│   │   ├── GroupConfig.cs
│   │   ├── GroupInfo.cs
│   │   └── ManagedServiceConfig.cs
│   ├── Services/
│   │   ├── Configuration/
│   │   │   ├── IConfigurationService.cs
│   │   │   └── ConfigurationService.cs
│   │   ├── Discovery/
│   │   │   ├── IServiceDiscoveryService.cs
│   │   │   └── DiscoveryService.cs
│   │   ├── Build/
│   │   │   ├── IBuildService.cs
│   │   │   └── BuildService.cs
│   │   ├── Git/
│   │   │   └── IGitService.cs
│   │   ├── HealthCheck/
│   │   │   └── IHealthChecker.cs
│   │   ├── ServiceControl/
│   │   │   ├── IServiceController.cs
│   │   │   ├── WindowsServiceController.cs
│   │   │   └── ProcessService.cs
│   │   ├── DialogService.cs
│   │   └── IDialogService.cs
│   ├── ViewModels/
│   │   ├── ViewModelBase.cs
│   │   ├── MainViewModel.cs
│   │   ├── ServiceItemViewModel.cs
│   │   ├── ServiceEditorViewModel.cs
│   │   ├── ServiceDiscoveryViewModel.cs
│   │   ├── DiscoveredServiceItemViewModel.cs
│   │   ├── GroupManagementViewModel.cs
│   │   ├── GroupItemViewModel.cs
│   │   ├── GitRepositoryEditorViewModel.cs
│   │   └── EnvironmentVariableViewModel.cs
│   ├── Views/
│   │   ├── MainWindow.xaml / .xaml.cs
│   │   ├── ServicesTab.xaml / .xaml.cs
│   │   ├── ServiceEditorDialog.xaml / .xaml.cs
│   │   ├── ServiceDiscoveryDialog.xaml / .xaml.cs
│   │   ├── GroupManagementDialog.xaml / .xaml.cs
│   │   └── GitRepositoryEditorDialog.xaml / .xaml.cs
│   ├── Controls/
│   │   └── SvgIcon.cs
│   ├── Themes/
│   │   ├── LightTheme.xaml
│   │   └── DarkTheme.xaml
│   └── Converters/
│       └── HexToBrushConverter.cs
├── tools/
│   └── ServiceDiscovery/
└── README.md
```

### 6.2 Class Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                           ViewModels                                 │
├─────────────────────────────────────────────────────────────────────┤
│ MainViewModel                                                        │
│ ─────────────────────────────────────────────────────────────────  │
│ + Services: ObservableCollection<ServiceItemViewModel>             │
│ + RefreshCommand, StartAllCommand, StopAllCommand                  │
│ + DiscoverServicesCommand, ManageGroupsCommand                     │
│ + AddServiceCommand, EditServiceCommand, DeleteServiceCommand      │
│ ─────────────────────────────────────────────────────────────────  │
│ + LoadConfiguration(), SaveConfiguration()                         │
│ + RefreshStatus(), StartAll(), StopAll()                           │
└─────────────────────────────────────────────────────────────────────┘
          │                       │                       │
          ▼                       ▼                       ▼
┌─────────────────────┐ ┌─────────────────────┐ ┌─────────────────────┐
│ServiceDiscoveryViewModel│ │ServiceEditorViewModel│ │GroupManagementViewModel│
│─────────────────────│ │─────────────────────│ │─────────────────────│
│+ DiscoverCommand    │ │+ AddNewGroupCommand │ │+ AddGroupCommand    │
│+ EditServiceCommand │ │+ BrowseExecutable   │ │+ RemoveGroupCommand │
│+ SelectAll/Deselect │ │+ BrowseWorkingDir   │ │+ SaveAllChanges     │
│+ FilterBy* Commands │ │+ ApplyTo(config)    │ │                     │
└─────────────────────┘ └─────────────────────┘ └─────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                            Models                                    │
├─────────────────────────────────────────────────────────────────────┤
│ AppData                                                             │
│ ─────────────────────────────────────────────────────────────────  │
│ + AppPilot: AppPilotSettings                                       │
│ + Services: List<ManagedServiceConfig>                             │
│ + Groups: List<GroupConfig>                                        │
│ + GitRepositories: List<GitRepositoryConfig>                       │
├─────────────────────────────────────────────────────────────────────┤
│ ManagedServiceConfig              GroupConfig                       │
│ ───────────────────────            ───────────                       │
│ + Name, DisplayName, Type         + Id (equals Name)                │
│ + ExecutablePath, Arguments       + Name                            │
│ + WorkingDirectory, CsprojPath    + ColorCode                       │
│ + Port, HealthCheckUrl            + DisplayOrder                    │
│ + GroupId, DisplayOrder                                               │
│ + Environment: Dict<string,string>                                  │
│ + Dependencies: List<string>                                        │
│ + UseWindowsService: bool                                           │
├─────────────────────────────────────────────────────────────────────┤
│ DiscoveredService                 GitRepositoryConfig               │
│ ───────────────────────            ────────────────────────        │
│ + ProjectPath, ProjectName         + Name, Path, Url                │
│ + DisplayName, Type                                                 │
│ + ExecutablePath, WorkingDirectory                                  │
│ + Port, HealthCheckUrl, Arguments                                  │
│ + EnvironmentVariables: Dict                                       │
│ + GrpcEndpoint, SwaggerUrl                                          │
│ + GroupId, DisplayOrder, IsSelected                                │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                          Services                                    │
├─────────────────────────────────────────────────────────────────────┤
│ ConfigurationService              DiscoveryService                  │
│ ───────────────────────            ──────────────────────          │
│ + Load(): AppData                  + DiscoverAsync(dir): Task       │
│ + Save(data): void                 (scans .csproj, launchSettings)  │
├─────────────────────────────────────────────────────────────────────┤
│ BuildService                      WindowsServiceController          │
│ ───────────────────────            ──────────────────────────────  │
│ + BuildAsync(csprojPath, output)   + Install/Uninstall/Start/Stop  │
│                                    + GetStatus(service)             │
├─────────────────────────────────────────────────────────────────────┤
│ ProcessService                    HttpHealthChecker                 │
│ ───────────────────────            ────────────────────────        │
│ + Start(config): Process            + CheckHealth(url): Task<bool>  │
│ + Stop(process): void                                                 │
│ + GetRunningProcess(name): Process                                    │
└─────────────────────────────────────────────────────────────────────┘
```

### 6.3 Data Flow

```
┌─────────────┐     ┌─────────────────┐     ┌──────────────────┐
│  AppData    │────▶│ Configuration   │────▶│ MainViewModel   │
│    .json    │     │   Service       │     │                  │
└─────────────┘     └─────────────────┘     └────────┬─────────┘
                                                       │
                                                       │ binds
                                                       ▼
┌─────────────┐     ┌─────────────────┐     ┌──────────────────┐
│   Windows   │◀────│ Service         │◀────│ Services Tab     │
│   Services  │     │ Controllers     │     │ (UI)             │
└─────────────┘     └─────────────────┘     └────────┬─────────┘
                                                       │
                                                       │ polls every 3s
                                                       ▼
┌─────────────┐     ┌─────────────────┐     ┌──────────────────┐
│  Processes  │◀────│ Process         │◀────│ ServiceStatus    │
│             │     │ Service         │     │ Checker          │
└─────────────┘     └─────────────────┘     └──────────────────┘

┌─────────────┐     ┌─────────────────┐     ┌──────────────────┐
│  .csproj    │────▶│ Discovery       │────▶│ ServiceDiscovery │
│  files      │     │ Service         │     │ Dialog           │
└─────────────┘     └─────────────────┘     └──────────────────┘
```

---

## 7. Configuration Schema

### 7.1 AppData.json

```json
{
  "AppPilot": {
    "BasePath": "D:\\Your\\Project\\Root",
    "PollingIntervalMs": 3000,
    "AutoStartServices": false,
    "MinimizeToTray": true,
    "LogDirectory": "Logs",
    "Theme": "Dark"
  },
  "Services": [
    {
      "Name": "AuthService",
      "DisplayName": "Auth Service",
      "Type": "Worker",
      "GroupId": "backend",
      "ExecutablePath": "AuthService\\bin\\Debug\\net10.0\\AuthService.exe",
      "Arguments": "",
      "WorkingDirectory": "AuthService\\bin\\Debug\\net10.0",
      "CsprojPath": "AuthService/AuthService.csproj",
      "Port": null,
      "HealthCheckUrl": "",
      "DisplayOrder": 1,
      "UseWindowsService": false,
      "Dependencies": [],
      "Environment": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    },
    {
      "Name": "GrpcGateway",
      "DisplayName": "gRPC Gateway",
      "Type": "Grpc",
      "GroupId": "backend",
      "ExecutablePath": "GrpcGateway\\bin\\Debug\\net10.0\\GrpcGateway.exe",
      "Arguments": "--urls=https://localhost:5002",
      "WorkingDirectory": "GrpcGateway\\bin\\Debug\\net10.0",
      "CsprojPath": "GrpcGateway/GrpcGateway.csproj",
      "Port": 5002,
      "HealthCheckUrl": "https://localhost:5002/health",
      "DisplayOrder": 2,
      "UseWindowsService": false,
      "Dependencies": ["AuthService"],
      "Environment": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "ASPNETCORE_Kestrel__Protocols": "Http2"
      }
    }
  ],
  "Groups": [
    {
      "Id": "backend",
      "Name": "Backend",
      "ColorCode": "#6366F1",
      "DisplayOrder": 1
    },
    {
      "Id": "frontend",
      "Name": "Frontend",
      "ColorCode": "#10B981",
      "DisplayOrder": 2
    }
  ],
  "GitRepositories": [
    {
      "Name": "Main Repository",
      "Path": "D:\\Projects\\MyApp",
      "Url": "https://github.com/org/myapp"
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
| GroupId | string | No | Group ID (equals group name). Empty = ungrouped |
| ExecutablePath | string | Yes | Path to the executable (relative to BasePath or absolute) |
| Arguments | string | No | Command line arguments (e.g., `--urls=https://localhost:5001`) |
| WorkingDirectory | string | No | Working directory for the process |
| CsprojPath | string | No | Path to .csproj file (required for build feature) |
| Port | int | No | Port number (for Grpc/WebApi) |
| HealthCheckUrl | string | No | URL for HTTP health check |
| DisplayOrder | int | No | Display order in the UI (default: 999) |
| UseWindowsService | bool | No | Whether to manage as Windows Service |
| Dependencies | string[] | No | List of service names this depends on |
| Environment | object | No | Environment variables (key-value pairs) |

### 7.3 Group Schema

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| Id | string | Yes | Group identifier (must equal Name) |
| Name | string | Yes | Display name (must be unique) |
| ColorCode | string | No | Hex color code (e.g., `#6366F1`) |
| DisplayOrder | int | Yes | Sort order in the UI |

---

## 8. API/Interface Definitions

### 8.1 Enums

```csharp
public enum ServiceType
{
    Worker,   // Windows Service or background worker
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
public class AppData
{
    public AppPilotSettings AppPilot { get; set; }
    public List<ManagedServiceConfig> Services { get; set; }
    public List<GroupConfig> Groups { get; set; }
    public List<GitRepositoryConfig> GitRepositories { get; set; }
}

public class AppPilotSettings
{
    public string BasePath { get; set; }
    public int PollingIntervalMs { get; set; } = 3000;
    public bool AutoStartServices { get; set; }
    public bool MinimizeToTray { get; set; } = true;
    public string LogDirectory { get; set; } = "Logs";
    public string Theme { get; set; } = "Dark";
}

public class ManagedServiceConfig
{
    public string Name { get; set; }
    public string DisplayName { get; set; }
    public ServiceType Type { get; set; }
    public string GroupId { get; set; }
    public string ExecutablePath { get; set; }
    public string Arguments { get; set; }
    public string WorkingDirectory { get; set; }
    public string CsprojPath { get; set; }
    public int? Port { get; set; }
    public string HealthCheckUrl { get; set; }
    public int DisplayOrder { get; set; }
    public bool UseWindowsService { get; set; }
    public List<string> Dependencies { get; set; }
    public Dictionary<string, string> Environment { get; set; }
}

public class GroupConfig
{
    public string Id { get; set; }      // Equals Name
    public string Name { get; set; }
    public string ColorCode { get; set; }
    public int DisplayOrder { get; set; }
}

public class GitRepositoryConfig
{
    public string Name { get; set; }
    public string Path { get; set; }
    public string Url { get; set; }
}

public class DiscoveredService
{
    public string ProjectPath { get; set; }
    public string ProjectName { get; set; }
    public string DisplayName { get; set; }
    public ServiceType Type { get; set; }
    public string ExecutablePath { get; set; }
    public string WorkingDirectory { get; set; }
    public string CsprojPath { get; set; }
    public int? Port { get; set; }
    public string HealthCheckUrl { get; set; }
    public string Arguments { get; set; }
    public Dictionary<string, string> EnvironmentVariables { get; set; }
    public bool UseWindowsService { get; set; }
    public string? GrpcEndpoint { get; set; }
    public string? SwaggerUrl { get; set; }
    public string GroupId { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsSelected { get; set; }
    public List<string> Dependencies { get; set; }
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
| Duplicate group name | Auto-select existing group instead of creating duplicate |
| Duplicate service name on import | Append `_1`, `_2` suffix automatically |
| Discovery finds no services | Display "No services found" message |
| launchSettings.json missing | Skip the project during discovery |

### 9.2 Logging

- Log all service operations (start, stop, install, uninstall, build)
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
| Notifications | Windows toast notifications on status change |
| Export/Import | Export/import configuration as separate file |
| Service Profiles | Save multiple configurations for different environments |
| Docker Compose Import | Import services from docker-compose.yml |

### 10.2 Known Limitations

- Does not support 32-bit executables (x64 only)
- Requires Windows 10/11 (not tested on Windows Server)
- No support for .NET Framework projects (only .NET 6+)
- Health check assumes Kestrel-based services
- Service discovery scans only 2 folder levels deep
- Group IDs must match group names (no separate GUID-based IDs)

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

### 11.3 Service Discovery

- [ ] Services are discovered from a selected directory
- [ ] Service types (Worker/gRPC/WebApi) are detected correctly
- [ ] Ports and endpoints are extracted from launchSettings.json
- [ ] Users can selectively import discovered services
- [ ] Discovered services can be edited before import
- [ ] Bulk group assignment works for selected services

### 11.4 Group Management

- [ ] Groups can be created, edited, and deleted
- [ ] Group names are unique (duplicates auto-select existing)
- [ ] Group colors can be set via picker or hex input
- [ ] Group display order is configurable
- [ ] Services display their group name (not ID) in the UI

### 11.5 UI/UX

- [ ] Status is displayed with appropriate colors and icons
- [ ] Actions are accessible via buttons
- [ ] Dark and light themes are supported
- [ ] Service editor allows full configuration editing
- [ ] New groups can be created inline from the service editor

### 11.6 Performance

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
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.2" />
    <PackageReference Include="Microsoft.Extensions.Configuration" Version="10.0.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="10.0.0" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.0" />
    <PackageReference Include="Microsoft.Extensions.Logging" Version="10.0.0" />
    <PackageReference Include="Serilog" Version="3.1.1" />
    <PackageReference Include="Serilog.Sinks.File" Version="5.0.0" />
    <PackageReference Include="System.ServiceProcess.ServiceController" Version="10.0.0" />
  </ItemGroup>
</Project>
```

### 12.2 Minimum System Requirements

| Requirement | Specification |
|-------------|---------------|
| OS | Windows 10 version 1809 or later |
| RAM | 4 GB (application uses ~100 MB) |
| Disk | 50 MB for application |
| .NET | .NET 10.0 Runtime |
| Privileges | Admin rights for Windows Service operations |

---

*Document Version: 2.0*
*Last Updated: April 2026*
