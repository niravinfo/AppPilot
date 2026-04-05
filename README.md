# AppPilot

AppPilot is a lightweight Windows desktop application for managing multiple .NET worker services, gRPC APIs, and Web APIs locally during development. It provides a unified UI to install, start, stop, build, and monitor the status of multiple .NET projects ideal for microservices development.

![Main Window](images/1%20-%20Main%20Window.png)

## Key Features

- **Unified Service Management:** Start, stop, and monitor .NET Worker, gRPC, and Web API projects from a single UI.
- **Windows Service Support:** Install/uninstall .NET Worker Services as Windows Services.
- **Service Discovery:** Automatically discover .NET services from a root directory — no manual configuration needed.
- **Group Management:** Organize services into custom groups with names, colors, and display order.
- **Individual Service Build:** Build any service directly from the UI using its `.csproj` file.
- **Dependency Handling:** Define and visualize service dependencies.
- **Environment Management:** Set environment variables per service.
- **Health Checks:** Monitor service health via configurable endpoints.
- **Auto-Start & Ordering:** Auto-start services and control their startup order.
- **Configurable via JSON:** All services, groups, and repositories are defined in easy-to-edit JSON files.

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

## Advanced Configuration

- Use `Groups` to organize services.
- Use `GitRepositories` to link code repositories for quick access.
- Use `Dependencies` to specify service startup order.

## Who Is It For?

- .NET developers working with multiple local services.
- Teams needing a simple, shareable way to manage dev environments.


## Example Config

See [`AppData.example.json`](src/AppData.example.json) for a full example.

