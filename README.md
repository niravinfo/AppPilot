# AppPilot

AppPilot is a powerful yet lightweight Windows desktop application for managing multiple .NET services, Node.js applications, and git repositories locally during development. It provides a unified UI to discover, configure, start, stop, build, and monitor the status of your entire development ecosystem — perfect for microservices and full-stack development.

![Main Window](images/1%20-%20Main%20Window.png)

## Key Features

### Service Management
- **Multi-Platform Support:** Manage .NET Worker Services, gRPC APIs, Web APIs, and Node.js/React applications from a single UI.
- **Windows Service Support:** Install/uninstall .NET Worker Services as Windows Services.
- **Process Management:** Start, stop, and restart services with real-time status monitoring.
- **Health Checks:** Monitor service health via configurable HTTP endpoints.
- **Service Discovery:** Automatically discover .NET services from any directory — no manual configuration needed.
- **Build Integration:** Build individual services or entire solutions directly from the UI.

### Organization & Workflow
- **Group Management:** Organize services into custom groups with colors, names, and display order.
- **Profile Management:** Create profiles to quickly start/stop subsets of services for different development scenarios.
- **Search & Filter:** Quickly find services with instant search across all tabs.
- **Service Dependencies:** Define startup dependencies to ensure correct initialization order.

### Git Integration
- **Repository Management:** Track multiple git repositories with branch and commit information.
- **Quick Git Operations:** Pull latest changes directly from the UI.
- **Linked Services:** Link services to repositories for coordinated builds and deployments.
- **Solution Builds:** Build entire solutions from repository configurations.

### Node.js/React Support
- **Custom npm Commands:** Configure and run npm scripts (build, start, serve, preview) with one click.
- **Project Management:** Manage Node.js applications alongside .NET services.
- **Visual Indicators:** Dedicated badges and colors for Node.js applications.

### Developer Experience
- **Light & Dark Themes:** Seamless theme switching with optimized color palettes.
- **Environment Variables:** Configure per-service environment variables.
- **Auto-Start & Ordering:** Define startup order and auto-start services on application launch.
- **Configurable Polling:** Adjust status refresh intervals to balance responsiveness and performance.
- **Minimize to Tray:** Run AppPilot in the background without cluttering your taskbar.
- **JSON Configuration:** All settings stored in easy-to-edit JSON files for version control and team sharing.

## Getting Started

### 1. Configure Your Services

Edit `AppData.json` to define your services, groups, and repositories. Example:

```json
{
	"AppPilot": {
		"BasePath": "D:\\Your\\Project\\Root"
	},
	"Services": [
		{
			"Name": "MyWorkerService",
			"DisplayName": "My Worker Service",
			"Type": "Worker",
			"GroupId": "workers",
			"ExecutablePath": "MyWorker\\bin\\Debug\\net10.0\\MyWorker.exe",
			"WorkingDirectory": "MyWorker\\bin\\Debug\\net10.0",
			"CsprojPath": "MyWorker/MyWorker.csproj",
			"Environment": {
				"ASPNETCORE_ENVIRONMENT": "Development"
			},
			"UseWindowsService": false
		}
		// ... more services ...
	],
	"Groups": [
		{
			"Id": "workers",
			"Name": "Workers",
			"DisplayOrder": 1,
			"ColorCode": "#6366F1"
		}
	]
}
```

- `CsprojPath` is required for the build feature.
- `ExecutablePath` and `WorkingDirectory` can be relative to `BasePath` or absolute.
- `GroupId` links a service to a group. If empty, the service is ungrouped.

### 2. Launch AppPilot

Run the AppPilot executable. The UI will display all configured services, their status, and available actions.

### 3. Managing Services

- **Start/Stop:** Use the UI buttons to start or stop any service.
- **Build:** Use the build button (visible if `CsprojPath` is set) to run `dotnet build` for that service.
- **Install/Uninstall:** For Worker services, install/uninstall as Windows Services.
- **Monitor:** View real-time status and health checks.

## Service Discovery

AppPilot can automatically discover .NET services from a directory, eliminating the need for manual configuration.

### How It Works

1. Click the **Discover** button in the toolbar.
2. Select a root directory to scan (or paste the path directly).
3. AppPilot scans up to 2 folder levels deep for `.csproj` files that have a `Properties/launchSettings.json`.
4. Discovered services are displayed in a categorized list:
   - **Workers** — detected by `Microsoft.NET.Sdk.Worker` or `OutputType=Exe` without ASP.NET Core references.
   - **gRPC** — detected by scanning `Program.cs` for `MapGrpcService` or `AddGrpc`.
   - **Web APIs** — all other ASP.NET Core projects.
5. Each discovered service shows its name, path, port, and type badge.

### Selective Import

- Use checkboxes to select which services to import.
- Use **Select All** / **Deselect All** to quickly manage selections (respects the active filter tab).
- Click **Import Selected** to add chosen services to your configuration.

### Editing Discovered Services

- Click the **pencil icon** on any discovered service row to open the full service editor.
- Edit display name, group, type, executable path, arguments, working directory, port, health check URL, environment variables, and dependencies.
- Changes are reflected immediately in the discovery list.

### Bulk Group Assignment

- Use the **Assign Group to Selected** panel to assign an existing group (or create a new one) to all selected services at once.
- Create new groups on the fly — if a group with the same name already exists, it will be auto-selected.
- Use the **Clear** button to remove group assignments from selected services.

## Profile Management

Profiles allow you to define subsets of services for different development scenarios (e.g., "Frontend Only", "Backend Services", "Full Stack"). 

### Creating and Using Profiles

1. Click the **Profiles** button in the toolbar to open the Profile Editor.
2. Create a new profile and give it a meaningful name and optional description.
3. Select which services should be included in this profile.
4. Mark a profile as "Default" to automatically load it on startup.
5. Use the profile dropdown in the main toolbar to switch between profiles quickly.

### Profile Operations

- **Switch Profiles:** Select a profile from the dropdown to filter services in the main view.
- **Start/Stop Profile:** Use group actions to start or stop all services in the active profile.
- **Edit Profiles:** Modify profile membership, reorder services, or change the default profile.
- **Default Profile:** The "Default (All Services)" profile shows all configured services.

Profiles are saved to `AppData.json` and persist across application restarts. The last selected profile is remembered.

## Git Repository Management

Track and manage your git repositories directly from AppPilot.

### Adding Repositories

1. Switch to the **Git Repos** tab.
2. Click **Add Repository** to open the repository editor.
3. Configure:
   - **Name:** Unique identifier for the repository.
   - **Display Name:** Friendly name shown in the UI.
   - **Local Path:** Path to your git repository folder.
   - **Solution Path:** Path to `.sln`, `.slnx`, or `.csproj` for the "Build Solution" feature.
   - **Default Branch:** The main branch (e.g., `main`, `master`, `develop`).
   - **Linked Services:** Associate services with this repository for coordinated operations.

### Repository Operations

- **Pull:** Fetch and merge the latest changes from the remote.
- **View Branch:** See the current active branch.
- **View Last Commit:** Display the most recent commit hash and message.
- **Build Solution:** Run `dotnet build` on the configured solution or project file.
- **Build/Restart Linked Services:** Quickly build and restart all services linked to this repository.
- **Open in Explorer:** Navigate to the repository folder.

Git repositories are configured in the `GitRepositories` section of `AppData.json`.

## Node.js and React Application Support

AppPilot fully supports Node.js and React applications alongside .NET services.

### Configuring Node.js Applications

1. Set the service `Type` to `NodeApp` in the Service Editor or `AppData.json`.
2. Configure the `ProjectPath` to point to your Node.js project folder (containing `package.json`).
3. Define custom npm commands in the `NpmCommands` array:

```json
{
  "Name": "MyReactApp",
  "DisplayName": "React Frontend",
  "Type": "NodeApp",
  "ProjectPath": "frontend",
  "NpmCommands": [
    { "Name": "Build", "Command": "npm run build" },
    { "Name": "Start", "Command": "npm run start" },
    { "Name": "Preview", "Command": "npm run preview" }
  ]
}
```

### Using npm Commands

- Each npm command appears as a clickable button on the Node.js service card.
- The first letter of the command name is shown on the button (e.g., "B" for Build, "S" for Start).
- Commands execute in the project's working directory with output logged to the AppPilot log directory.

**Note:** Node.js applications don't have traditional start/stop controls since they're managed via npm commands.

## Theme Support

AppPilot supports both Light and Dark themes with carefully optimized color palettes.

### Switching Themes

- **Quick Toggle:** Click the theme toggle button (sun/moon icon) in the toolbar to instantly switch themes.
- **Settings:** Open Settings to view and change the theme, with a preview of your selection.

### Theme Features

- **Optimized Colors:** Distinct color schemes for service types, groups, and status indicators in both themes.
- **Automatic Persistence:** Your theme preference is saved and restored on application restart.
- **Performance Optimized:** Cached brushes ensure smooth theme transitions without memory overhead.

The current theme is stored in `ui-settings.json` in the application directory.

## Group Management

Organize your services into logical groups for better visual organization.

### Creating and Managing Groups

1. Click the **Groups** button in the toolbar to open the Group Management dialog.
2. Use the **Add Group** input at the top to create new groups by name.
3. Each group row shows:
   - **Name** — editable inline.
   - **Color** — click the color swatch to pick from a preset palette, or enter a hex code manually.
   - **Order** — controls display order in the main UI.
   - **Services** — count of services assigned to the group.
4. Click **Save All Changes** to persist groups to `AppData.json`.

### Group Rules

- Group names must be unique. If you try to create a group with an existing name, the existing group is automatically selected.
- Group IDs are set to match the group name (no GUIDs).
- New groups are assigned `DisplayOrder = max existing order + 1`.
- Ungrouped services appear under "Ungrouped" in the main UI.

## Settings & Configuration

### Application Settings

Access Settings via the toolbar button to configure:

- **Polling Interval:** How frequently AppPilot checks service status (1-3600 seconds). Default: 30 seconds.
- **Log Directory:** Where service logs are stored. Default: `Logs` folder in the application directory.
- **Theme:** Choose between Light and Dark themes.
- **Application Info:** View version number, author, and GitHub repository link.

Settings are saved immediately and most take effect without restarting the application.

### Configuration Files

AppPilot uses two main configuration files:

1. **`AppData.json`** — Stores all service, group, repository, and profile configurations.
   - Located in the application directory or at a custom path (configured via `AppPilot.ConfigurationPath`).
   - Can be version-controlled and shared across teams.
   - Supports relative paths (relative to `AppPilot.BasePath`) or absolute paths.

2. **`ui-settings.json`** — Stores UI preferences (theme).
   - Located in the application directory.
   - Automatically created and managed by AppPilot.

### Environment Variables

Set environment variables for any service:

1. Open the Service Editor for a service.
2. Add key-value pairs in the Environment Variables section.
3. Variables are applied when the service starts.

Common examples:
- `ASPNETCORE_ENVIRONMENT=Development`
- `DOTNET_ENVIRONMENT=Development`
- `ConnectionStrings__DefaultConnection=Server=localhost;...`

## Advanced Configuration

### Service Dependencies

Define dependencies to ensure services start in the correct order:

```json
{
  "Name": "WebApi",
  "Dependencies": ["DatabaseService", "CacheService"]
}
```

AppPilot will ensure dependent services start first.

### Auto-Start Configuration

Use `DisplayOrder` to control startup sequence when starting multiple services:

```json
{
  "Name": "DatabaseService",
  "DisplayOrder": 1
},
{
  "Name": "WebApi",
  "DisplayOrder": 2
}
```

### Health Check Configuration

Configure health check URLs for automatic monitoring:

```json
{
  "Name": "MyApi",
  "Port": 5000,
  "HealthCheckUrl": "http://localhost:5000/health"
}
```

If `HealthCheckUrl` is not specified but `Port` is set, AppPilot generates a default URL (`http://localhost:{Port}/`).

## Who Is It For?

- **.NET Developers:** Working with multiple microservices, Worker Services, gRPC, or Web APIs.
- **Full-Stack Developers:** Managing both .NET backend services and Node.js/React frontend applications.
- **Team Leads:** Needing a shareable, version-controlled way to standardize local development environments.
- **DevOps Engineers:** Prototyping and testing service orchestration locally before deploying to production.
- **Anyone** who wants a simple, unified interface for managing complex local development setups.

## Example Config

A comprehensive example showing all features:

```json
{
  "AppPilot": {
    "BasePath": "D:\\MyProjects",
    "PollingIntervalMs": 30000,
    "MinimizeToTray": true,
    "LogDirectory": "Logs",
    "LastSelectedProfileId": "frontend-dev"
  },
  "Services": [
    {
      "Name": "AuthService",
      "DisplayName": "Authentication Service",
      "Type": "Worker",
      "GroupId": "backend",
      "ExecutablePath": "AuthService\\bin\\Debug\\net10.0\\AuthService.exe",
      "WorkingDirectory": "AuthService\\bin\\Debug\\net10.0",
      "CsprojPath": "AuthService/AuthService.csproj",
      "Port": 5001,
      "HealthCheckUrl": "http://localhost:5001/health",
      "DisplayOrder": 1,
      "Environment": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "ConnectionStrings__Redis": "localhost:6379"
      },
      "UseWindowsService": false
    },
    {
      "Name": "ApiGateway",
      "DisplayName": "API Gateway",
      "Type": "WebApi",
      "GroupId": "backend",
      "ExecutablePath": "Gateway\\bin\\Debug\\net10.0\\Gateway.exe",
      "WorkingDirectory": "Gateway\\bin\\Debug\\net10.0",
      "CsprojPath": "Gateway/Gateway.csproj",
      "Port": 5000,
      "HealthCheckUrl": "http://localhost:5000/health",
      "DisplayOrder": 2,
      "Dependencies": ["AuthService"],
      "Environment": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    },
    {
      "Name": "OrderService",
      "DisplayName": "Order Service (gRPC)",
      "Type": "Grpc",
      "GroupId": "backend",
      "ExecutablePath": "OrderService\\bin\\Debug\\net10.0\\OrderService.exe",
      "WorkingDirectory": "OrderService\\bin\\Debug\\net10.0",
      "CsprojPath": "OrderService/OrderService.csproj",
      "Port": 5002,
      "DisplayOrder": 3
    },
    {
      "Name": "ReactFrontend",
      "DisplayName": "React Dashboard",
      "Type": "NodeApp",
      "GroupId": "frontend",
      "ProjectPath": "dashboard-ui",
      "DisplayOrder": 10,
      "NpmCommands": [
        { "Name": "Build", "Command": "npm run build" },
        { "Name": "Start", "Command": "npm run dev" },
        { "Name": "Test", "Command": "npm test" },
        { "Name": "Lint", "Command": "npm run lint" }
      ]
    }
  ],
  "Groups": [
    {
      "Id": "backend",
      "Name": "Backend Services",
      "DisplayOrder": 1,
      "ColorCode": "#6366F1"
    },
    {
      "Id": "frontend",
      "Name": "Frontend Apps",
      "DisplayOrder": 2,
      "ColorCode": "#22C55E"
    }
  ],
  "GitRepositories": [
    {
      "Name": "main-repo",
      "DisplayName": "Main Solution",
      "LocalPath": "D:\\MyProjects\\MainSolution",
      "SolutionPath": "MainSolution.slnx",
      "DefaultBranch": "main",
      "LinkedServiceNames": ["AuthService", "ApiGateway", "OrderService"]
    },
    {
      "Name": "frontend-repo",
      "DisplayName": "Dashboard UI",
      "LocalPath": "D:\\MyProjects\\dashboard-ui",
      "DefaultBranch": "develop",
      "LinkedServiceNames": ["ReactFrontend"]
    }
  ],
  "Profiles": [
    {
      "Id": "frontend-dev",
      "Name": "Frontend Development",
      "Description": "Frontend with minimal backend",
      "IsDefault": true,
      "DisplayOrder": 1,
      "ServiceNames": ["ApiGateway", "ReactFrontend"]
    },
    {
      "Id": "full-stack",
      "Name": "Full Stack",
      "Description": "All services for full-stack development",
      "IsDefault": false,
      "DisplayOrder": 2,
      "ServiceNames": ["AuthService", "ApiGateway", "OrderService", "ReactFrontend"]
    },
    {
      "Id": "backend-only",
      "Name": "Backend Only",
      "Description": "All backend services for API development",
      "IsDefault": false,
      "DisplayOrder": 3,
      "ServiceNames": ["AuthService", "ApiGateway", "OrderService"]
    }
  ]
}
```

See [`AppData.example.json`](src/AppData.example.json) for a full example.

---

## Screenshots

### Main Services Tab
![Main Window](images/1%20-%20Main%20Window.png)

### Git Repositories Tab
Manage your repositories, pull changes, and build solutions.

### Service Discovery
Automatically discover services from any directory.

### Dark Theme
Seamless dark mode for late-night coding sessions.

---

## Tips & Best Practices

1. **Use Profiles:** Create profiles for different development scenarios (frontend-only, backend-only, full-stack).
2. **Version Control:** Commit `AppData.json` to your repository so the entire team shares the same configuration.
3. **Relative Paths:** Use relative paths (relative to `BasePath`) for portability across different machines.
4. **Health Checks:** Configure health check URLs to get instant feedback on service availability.
5. **Service Discovery:** Use the discovery feature to quickly onboard new projects instead of manual configuration.
6. **Git Integration:** Link services to repositories to build and restart multiple related services with one click.
7. **Group Organization:** Use groups and colors to visually separate different layers (database, backend, frontend, infrastructure).
8. **npm Commands:** Customize npm commands for your Node.js projects beyond the defaults (e.g., "Storybook", "E2E Tests").

---

## Requirements

- **OS:** Windows 10/11
- **.NET Runtime:** .NET 10 or later
- **Git:** Required for Git repository features
- **Node.js:** Required for managing Node.js/React applications

---

## Contributing

Contributions are welcome! Please feel free to submit issues or pull requests on [GitHub](https://github.com/niravinfo/AppPilot).

### Feature Requests

Have ideas for new features? Check out our [Feature Ideas](FEATURE_IDEAS.md) document for planned enhancements, or suggest your own!

---

## License

See [LICENSE.txt](LICENSE.txt) for details.

---

## Author

**Nirav Patel**

For questions, suggestions, or issues, visit the [GitHub repository](https://github.com/niravinfo/AppPilot).

