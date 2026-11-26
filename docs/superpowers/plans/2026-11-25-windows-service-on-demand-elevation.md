# On-demand Windows Service Elevation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace startup-wide elevation with a one-per-session elevated broker used only by Windows-service operations.

**Architecture:** The WPF app runs as `asInvoker`. On the first privileged service action it launches a dedicated `requireAdministrator` console broker via `runas` and reuses it over an ACL-restricted named pipe for the rest of the session. The main app keeps unelevated status reads and delegates install/uninstall/start/stop to the broker.

**Tech Stack:** .NET 10, WPF, named pipes, `System.ServiceProcess.ServiceController`, `sc.exe`, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-25-windows-service-on-demand-elevation-design.md`

## Global Constraints

- Target framework `net10.0-windows`, RID `win-x64`, .NET 10 packages only.
- Main app manifest `asInvoker`; broker manifest `requireAdministrator`.
- Broker accepts only `BrokerOperation` values; never a raw command string.
- Pipe ACL grants access only to the caller-supplied SID; connecting PID must match.
- `runas` launches must set `UseShellExecute = true` and use an absolute exe path.
- Broker exe must land next to `AppPilot.exe` on build and publish.
- No changes to process-based service management or public `IServiceController` signatures.

## Review Focus

- Declining UAC must leave the app open and change no service state — covered by `AdminBrokerClientTests.CanceledElevation_IsReportedWithoutStartingPipe` (Task 3).
- A broker that dies mid-session must be replaced on the next action — covered by `AdminBrokerClientTests.PipeFailure_ResetsBroker` (Task 3).
- A forged or unknown operation must be rejected by the broker — covered by `BrokerProtocolTests.UnknownOperation_IsRejected` (Task 1) and the allowlist dispatch in Task 2.
- Service names from user-editable config must not inject extra `sc.exe` arguments — covered by `BrokerProtocolTests.UnsafeServiceName_IsRejected` (Task 1) and `ScArgumentBuilderTests`.
- App exit must not leave an elevated broker behind — covered by Task 4's `App.OnExit` wiring and Task 5's manual check.

---

### Task 1: Broker project, protocol and test harness

**Files:**
- Create: `src/AppPilot.AdminBroker/AppPilot.AdminBroker.csproj`
- Create: `src/AppPilot.AdminBroker/app.manifest`
- Create: `src/AppPilot.AdminBroker/BrokerProtocol.cs`
- Create: `tests/AppPilot.Tests/AppPilot.Tests.csproj`
- Create: `tests/AppPilot.Tests/BrokerProtocolTests.cs`
- Modify: `src/AppPilot.csproj`, `src/AppPilot.slnx`

**Interfaces:**
- Produces: `BrokerOperation` enum (`Install`, `Uninstall`, `Start`, `Stop`, `Shutdown`), `BrokerRequest`, `BrokerResponse`, `BrokerProtocol.IsValidServiceName(string)`, `BrokerProtocol.PipePrefix`.

- [x] **Step 1: Create broker project and manifest**

`src/AppPilot.AdminBroker/AppPilot.AdminBroker.csproj`: `OutputType=Exe`, `TargetFramework=net10.0-windows`, `RuntimeIdentifier=win-x64`, `AssemblyName=AppPilot.AdminBroker`, `ApplicationManifest=app.manifest`. `app.manifest`: copy `src/app.manifest` but with `requireAdministrator`.

- [x] **Step 2: Create the protocol types in `BrokerProtocol.cs`**

```csharp
public enum BrokerOperation { Install, Uninstall, Start, Stop, Shutdown }
public sealed class BrokerRequest
{
    public BrokerOperation Operation { get; set; }
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string ExecutablePath { get; set; } = "";
    public string Arguments { get; set; } = "";
}
public sealed class BrokerResponse
{
    public bool Success { get; set; }
    public string Error { get; set; } = "";
}
public static class BrokerProtocol
{
    public const string PipePrefix = "AppPilot.AdminBroker.";
    public static bool IsValidServiceName(string? name);
}
```

`IsValidServiceName` returns true only for 1–80 chars of letters, digits, `.`, `_`, `-` and spaces, with no quote, backslash, slash or control characters.

- [x] **Step 3: Write failing protocol tests**

`tests/AppPilot.Tests/BrokerProtocolTests.cs` with xUnit:
- `Request_RoundTripsThroughJson`: serialize `BrokerRequest` with `JsonStringEnumConverter`, deserialize, assert operation and all four string fields equal.
- `UnknownOperation_IsRejected`: `(BrokerOperation)99` deserialization does not produce a valid allowlisted operation (assert `Enum.IsDefined` false).
- `UnsafeServiceName_IsRejected`: Theory with `""`, `"a\"b"`, `"a\\b"`, `"a/b"`, `"a\nb"`, `"a;b"`, and 81 chars — all false; `"My.Service_1-2"` true.
- `PipePrefix_IsNamespaced`: assert `BrokerProtocol.PipePrefix == "AppPilot.AdminBroker."`.

- [x] **Step 4: Create the xUnit test project**

`tests/AppPilot.Tests/AppPilot.Tests.csproj`: `net10.0-windows`, references `src/AppPilot.csproj` and `src/AppPilot.AdminBroker/AppPilot.AdminBroker.csproj`; packages `xunit` 2.9.3, `xunit.runner.visualstudio` 2.8.2, `Microsoft.NET.Test.Sdk` 17.14.1.

- [x] **Step 5: Run tests and see them fail**

Run: `dotnet test tests/AppPilot.Tests/AppPilot.Tests.csproj`
Expected: FAIL — broker project/types not found.

- [x] **Step 6: Wire projects and make tests pass**

Add to `src/AppPilot.csproj`:
```xml
<ProjectReference Include="..\AppPilot.AdminBroker\AppPilot.AdminBroker.csproj" />
```
Add both projects to `src/AppPilot.slnx`. Run `dotnet test` again.
Expected: PASS (4 tests).

- [x] **Step 7: Commit**

```bash
git add src/AppPilot.AdminBroker src/AppPilot.csproj src/AppPilot.slnx tests/AppPilot.Tests
git commit -m "feat: add admin broker project and protocol"
```

---

### Task 2: Broker endpoint and privileged operation handlers

**Files:**
- Create: `src/AppPilot.AdminBroker/ScArgumentBuilder.cs`
- Create: `src/AppPilot.AdminBroker/ServiceOperations.cs`
- Create: `src/AppPilot.AdminBroker/Program.cs`
- Create: `tests/AppPilot.Tests/ScArgumentBuilderTests.cs`

**Interfaces:**
- Consumes: `BrokerRequest`, `BrokerResponse`, `BrokerOperation`, `BrokerProtocol.IsValidServiceName` from Task 1.
- Produces: `ScArgumentBuilder.Create(string name, string executablePath, string arguments)`, `.Description(string name, string displayName)`, `.Delete(string name)`, `.Start(string name)`, `.Stop(string name)` each returning `string[]`; `ServiceOperations.ExecuteAsync(BrokerRequest request) : Task<BrokerResponse>`.

- [x] **Step 1: Write failing argument-builder tests**

`ScArgumentBuilderTests`:
- `Create_UsesDemandStart`: array is `["create", name, "binPath=", "<exe> <args>", "start=", "demand"]`.
- `Create_QuotesAreHandledByArgumentList`: assert no element contains an embedded `"` used for shell quoting.
- `Delete_And_Start_And_Stop_AreSingleVerbCommands`: assert exact arrays.

- [x] **Step 2: Run tests and see them fail**

Run: `dotnet test tests/AppPilot.Tests/AppPilot.Tests.csproj --filter FullyQualifiedName~ScArgumentBuilderTests`
Expected: FAIL — type not found.

- [x] **Step 3: Implement `ScArgumentBuilder`**

Use `System.Diagnostics.ProcessStartInfo.ArgumentList` semantics: return argument tokens without shell quotes.

- [x] **Step 4: Implement `ServiceOperations.ExecuteAsync`**

Allowlist switch on `request.Operation`:
- `Install`: validate name; `sc.exe create` then `sc.exe description` (description failure is non-fatal); return success/error.
- `Uninstall`: validate name; stop if running, then `sc.exe delete`.
- `Start` / `Stop`: `sc.exe start|stop`, then poll `ServiceController.Status` up to 30 seconds for `Running` / `Stopped`; a service that returns to `Stopped` during start is an error.
- `Shutdown`: handled by `Program`, not here.
- Unknown operation: return `BrokerResponse` with `Success=false`, `Error="unsupported_operation"`.

All `sc.exe` launches use `UseShellExecute=false`, `RedirectStandardOutput/Error=true`, `CreateNoWindow=true`, and `ArgumentList`.

- [x] **Step 5: Implement `Program.cs`**

Parse `--pipe`, `--client-pid`, `--client-sid` (exit 2 if missing/invalid). Build `PipeSecurity` granting `ReadWrite` to the supplied SID. Create the server with `NamedPipeServerStreamAcl.Create` and a unique pipe name. Wait up to 30 s for a connection; verify `GetNamedPipeClientProcessId()` matches; then loop reading one JSON request per line, dispatch via `ServiceOperations.ExecuteAsync`, write one JSON response per line. Exit on `Shutdown`, client disconnect, or parent exit. Never use `PipeOptions.CurrentUserOnly`.

- [x] **Step 6: Run tests and see them pass**

Run: `dotnet test tests/AppPilot.Tests/AppPilot.Tests.csproj`
Expected: PASS.

- [x] **Step 7: Commit**

```bash
git add src/AppPilot.AdminBroker tests/AppPilot.Tests
git commit -m "feat: add elevated broker endpoint and service handlers"
```

---

### Task 3: Main-app broker client

**Files:**
- Create: `src/Services/ServiceControl/IAdminBrokerClient.cs`
- Create: `src/Services/ServiceControl/BrokerPipeClient.cs`
- Create: `src/Services/ServiceControl/AdminBrokerClient.cs`
- Create: `tests/AppPilot.Tests/AdminBrokerClientTests.cs`

**Interfaces:**
- Produces: `AdminBrokerResult(bool Success, string Error, bool WasCancelled)`, `IAdminBrokerClient.ExecuteAsync(BrokerOperation, ManagedServiceConfig, CancellationToken)`.
- Consumes: `BrokerRequest`, `BrokerResponse`, `BrokerProtocol` from Task 1.

- [x] **Step 1: Write failing pipe-client tests**

`tests/AppPilot.Tests/AdminBrokerClientTests.cs` starts a real `NamedPipeServerStream` on a test pipe name on a background task:
- `SendAsync_ReturnsBrokerResponse`: server reads one `BrokerRequest`, asserts operation/name, writes `BrokerResponse { Success = true }`; client `SendAsync` returns success.
- `PipeFailure_ResetsBroker`: with the server disposed, `SendAsync` throws `IOException`; a following `SendAsync` uses a fresh connection.
- `CanceledElevation_IsReportedWithoutStartingPipe`: inject a fake launcher whose `Start` throws `Win32Exception(1223)`; `ExecuteAsync` returns `WasCancelled = true` and no pipe connection is attempted.

- [x] **Step 2: Run tests and see them fail**

Run: `dotnet test tests/AppPilot.Tests/AppPilot.Tests.csproj --filter FullyQualifiedName~AdminBrokerClientTests`
Expected: FAIL — types not found.

- [x] **Step 3: Define `IAdminBrokerClient` and `AdminBrokerResult`**

`AdminBrokerResult` is a record with `Success`, `Error`, `WasCancelled`. `IAdminBrokerClient : IDisposable`.

- [x] **Step 4: Implement `BrokerPipeClient`**

`Task<BrokerResponse> SendAsync(BrokerRequest request, CancellationToken ct)`: connect if needed (10 s), write JSON line, read JSON line (60 s), deserialize with `JsonStringEnumConverter`, disconnect on any `IOException`/timeout.

- [x] **Step 5: Implement `AdminBrokerClient`**

Lazy broker start on first call: pipe name `BrokerProtocol.PipePrefix + Environment.ProcessId + "." + Guid.NewGuid():N`; SID from `WindowsIdentity.GetCurrent().User`; absolute path `Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppPilot.AdminBroker.exe")`; `UseShellExecute=true`, `Verb="runas"`, `WindowStyle=Hidden`. Catch `Win32Exception` 1223 → `WasCancelled`. Serialize calls with `SemaphoreSlim`; on pipe failure dispose and null the client so the next call re-prompts. `Dispose()` sends `Shutdown`, then kills the broker if still alive.

- [x] **Step 6: Run tests and see them pass**

Run: `dotnet test tests/AppPilot.Tests/AppPilot.Tests.csproj`
Expected: PASS (all tests).

- [x] **Step 7: Commit**

```bash
git add src/Services/ServiceControl tests/AppPilot.Tests
git commit -m "feat: add reusable elevated broker client"
```

---

### Task 4: Refactor controller, wire DI, remove startup elevation

**Files:**
- Modify: `src/Services/ServiceControl/WindowsServiceController.cs`
- Modify: `src/App.xaml.cs`
- Modify: `src/app.manifest`
- Create: `tests/AppPilot.Tests/WindowsServiceControllerTests.cs`

**Interfaces:**
- Consumes: `IAdminBrokerClient` from Task 3; existing `IServiceController` unchanged.

- [x] **Step 1: Write failing controller tests**

With a fake `IAdminBrokerClient`:
- `InstallAsync_DelegatesInstall`: assert captured operation `Install` and config identity; result success returns true.
- `StartAsync_BrokerFailure_Throws`: broker returns failure; `StartAsync` throws `InvalidOperationException` with the broker error.
- `StopAsync_DeclinedElevation_ThrowsWithClearMessage`: `WasCancelled=true`; message contains "Administrator permission was declined".
- `GetStatus_DoesNotTouchBroker`: fake call count remains zero after `GetStatus`.

- [x] **Step 2: Run tests and see them fail**

Run: `dotnet test tests/AppPilot.Tests/AppPilot.Tests.csproj --filter FullyQualifiedName~WindowsServiceControllerTests`
Expected: FAIL — constructor signature mismatch.

- [x] **Step 3: Refactor `WindowsServiceController`**

Inject `IAdminBrokerClient` alongside `ILogger<WindowsServiceController>`. Replace `sc.exe`/`ServiceController.Start()/Stop()` calls with broker calls; keep `GetStatus()` local. Map `WasCancelled` to the declined-permission message. Keep existing exception types so `ServiceItemViewModel` error handling is unchanged.

- [x] **Step 4: Update `App.xaml.cs` and `app.manifest`**

Remove `IsRunningAsAdministrator`, `RestartAsAdministrator`, the startup elevation branch and now-unused usings. Register `services.AddSingleton<IAdminBrokerClient, AdminBrokerClient>()`. Change `requestedExecutionLevel` to `asInvoker`.

- [x] **Step 5: Run tests and see them pass**

Run: `dotnet test tests/AppPilot.Tests/AppPilot.Tests.csproj`
Expected: PASS.

- [x] **Step 6: Commit**

```bash
git add src/Services/ServiceControl/WindowsServiceController.cs src/App.xaml.cs src/app.manifest tests/AppPilot.Tests
git commit -m "feat: route Windows service actions through the elevated broker"
```

---

### Task 5: Build/publish verification

**Files:** none (verification only)

- [x] **Step 1: Build with warnings as errors**

Run: `dotnet build src/AppPilot.csproj -warnaserror`
Expected: PASS, and `AppPilot.AdminBroker.exe` exists in `src/bin/Debug/net10.0-windows/win-x64/`.

- [x] **Step 2: Publish and verify broker is present**

Run: `dotnet publish src/AppPilot.csproj -c Release -o <temp>`
Expected: both `AppPilot.exe` and `AppPilot.AdminBroker.exe` in the output.

- [x] **Step 3: Full test run**

Run: `dotnet test tests/AppPilot.Tests/AppPilot.Tests.csproj`
Expected: PASS.

- [ ] **Step 4: Manual smoke checklist (NOT COMPLETED — requires an interactive elevated desktop)**

Launch AppPilot as a normal user → no UAC. Start a process-based service → no UAC. Install/start/stop a Windows service → exactly one UAC prompt for the session, action succeeds; second action → no prompt. Cancel UAC → app stays open with clear error. Close app → broker process exits.

Status: cannot be executed in this environment. The human partner must run this on a real desktop before merge.

- [x] **Step 5: Commit**

```bash
git add -A
git commit -m "chore: verify on-demand elevation build and behavior"
```
