# AppPilot

AppPilot is a lightweight Windows desktop application for managing multiple .NET worker services, gRPC APIs, and Web APIs locally during development. It provides a unified UI to install, start, stop, build, and monitor the status of multiple .NET projects ideal for microservices development.

## Key Features

- **Unified Service Management:** Start, stop, and monitor .NET Worker, gRPC, and Web API projects from a single UI.
- **Windows Service Support:** Install/uninstall .NET Worker Services as Windows Services.
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
			"GroupId": "workers_01",
			"ExecutablePath": "MyWorker\\bin\\Debug\\net10.0\\MyWorker.exe",
			"WorkingDirectory": "MyWorker\\bin\\Debug\\net10.0",
			"CsprojPath": "MyWorker/MyWorker.csproj",
			"Environment": {
				"ASPNETCORE_ENVIRONMENT": "Development"
			},
			"UseWindowsService": false
		}
		// ... more services ...
	]
}
```

- `CsprojPath` is required for the build feature.
- `ExecutablePath` and `WorkingDirectory` can be relative to `BasePath` or absolute.

### 2. Launch AppPilot

Run the AppPilot executable. The UI will display all configured services, their status, and available actions.

### 3. Managing Services

- **Start/Stop:** Use the UI buttons to start or stop any service.
- **Build:** Use the build button (visible if `CsprojPath` is set) to run `dotnet build` for that service.
- **Install/Uninstall:** For Worker services, install/uninstall as Windows Services.
- **Monitor:** View real-time status and health checks.

### 4. Advanced Configuration

- Use `Groups` to organize services.
- Use `GitRepositories` to link code repositories for quick access.
- Use `Dependencies` to specify service startup order.

## Who Is It For?

- .NET developers working with multiple local services.
- Teams needing a simple, shareable way to manage dev environments.


## Example Config

See [`AppData.example.json`](https://github.com/niravinfo/AppPilot/blob/main/src/AppData.example.json) for a full example.