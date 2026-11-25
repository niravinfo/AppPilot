# On-demand admin elevation for Windows service operations

Date: 2026-11-25
Branch: `feature/on-demand-windows-service-elevation`

## Intent and why

AppPilot currently declares `requireAdministrator` in `src/app.manifest` and
`App.OnStartup` relaunches itself with `runas` whenever the token is not
elevated. Every launch therefore shows UAC, even though the vast majority of
users only manage ordinary processes (Worker / WebApi / gRPC / Node) and never
touch a Windows service.

Only `WindowsServiceController` actually needs admin rights: `sc.exe create` /
`delete` and `ServiceController.Start()` / `Stop()`. Status reads work as a
normal user.

Goal: **zero UAC prompts for the normal path, one prompt per session for users
who actually manage Windows services, and no app restart that can lose a
pending action.**

## Goals

- Main UI runs as `asInvoker` and never restarts itself for elevation.
- The first Windows-service action starts a dedicated elevated broker once;
  subsequent actions in the same session reuse it with no further prompts.
- The broker accepts a closed set of typed operations, never raw command lines.
- Declining UAC leaves the app open, reports a clear error, and changes nothing.
- Windows-service status continues to be read unelevated from the main app.

## Non-goals

- No change to process-based service management.
- No installer / SYSTEM service installed at setup time (would change the
  product's lightweight install story).
- No support for managing services the current user has no rights to at all
  (a standard user without an admin credential still cannot elevate).

## Anti-goals

- Bringing back a self-restart that carries in-memory intent — this is exactly
  what failed previously ("it forgot what to start or stop").
- Reusing the WPF executable as the elevated helper — that is what produced the
  "App freeze - not working" commits on `origin/feature/OnDemandUAC`.
- Prompting per action (annoying), or prompting at launch (punishes 95% of
  users).

## Constraints

- .NET 10, WPF, `net10.0-windows`, `win-x64`; no new runtime dependencies.
- Existing DI in `App.OnStartup`; existing `IServiceController` interface and
  callers in `MainViewModel` / `ServiceItemViewModel` must keep working.
- The broker EXE must be present next to `AppPilot.exe` in build and publish
  output (via a project reference).
- Named pipe ACL must be explicit; `PipeOptions.CurrentUserOnly` cannot be used
  for medium → high integrity communication.

## Approaches considered

1. **Per-action `sc.exe` with `runas`** — small, no restart, but one UAC prompt
   per action. Rejected: user experience.
2. **Persist intent + self-restart elevated** — one prompt, but visible restart,
   two instances, lost UI state, larger elevated attack surface. Rejected:
   it repeats the previously failed shape.
3. **Dedicated elevated broker EXE + named pipe (chosen)** — one prompt per
   session, no restart, UI stays at least privilege. Costs one small extra
   executable. This matches Microsoft's "Administrator Broker Model" and how
   Docker Desktop isolates privileged work.
4. **Drop Windows-service support** — simplest, but removes a documented
   feature. Rejected for now.

All four were discussed with the product owner; option 3 was selected.

## Design

### New executable: `AppPilot.AdminBroker`

- Console app, `net10.0-windows`, `win-x64`, own `app.manifest` with
  `requireAdministrator`.
- Launched by the main app with `--pipe <name> --client-pid <pid> --client-sid <sid>`.
- Creates a named pipe whose ACL grants access only to the supplied client SID;
  verifies `GetNamedPipeClientProcessId()` equals the supplied PID.
- Reads newline-delimited JSON requests, dispatches through an operation
  allowlist (`install`, `uninstall`, `start`, `stop`, `shutdown`), writes a
  newline-delimited JSON response.
- Exits on `shutdown`, on client disconnect plus parent-process exit, or after
  a connection timeout.

### Shared protocol

Public types hosted in the broker assembly and referenced by the main app:
`BrokerOperation` (enum), `BrokerRequest` (operation + name + displayName +
executablePath + arguments), `BrokerResponse` (success + error),
`BrokerProtocol` (pipe prefix constants).

### Client: `IAdminBrokerClient` in the main app

- Lazily starts the broker on first privileged call with
  `UseShellExecute = true`, `Verb = "runas"`, absolute path, hidden window.
  `Win32Exception.NativeErrorCode == 1223` means the user declined UAC.
- Connects once, reuses the pipe for the rest of the session, serializes
  requests with a semaphore, and applies connect/command timeouts.
- Any pipe failure tears down the broker so the next action can prompt again.
- Disposed with the DI container at app exit (sends `shutdown`).

### `WindowsServiceController`

- Keeps `GetStatus()` local (unelevated).
- `InstallAsync` / `UninstallAsync` / `StartAsync` / `StopAsync` delegate to
  `IAdminBrokerClient` and surface failures as `InvalidOperationException`,
  with a distinct message when elevation was declined.
- No `sc.exe` invocation remains in the main executable.

### `App`

- Manifest becomes `asInvoker`; `IsRunningAsAdministrator` /
  `RestartAsAdministrator` are removed.
- Registers `IAdminBrokerClient` as a singleton.

### UI behavior

No new UI is required for v1: the existing error message area shows broker
failures. A UAC shield is not added to buttons in this change.

## Failure modes to handle

- UAC declined → app stays open, no state change, clear error.
- Broker exe missing / fails to start → clear error, no state change.
- Broker crashes mid-session → next action restarts it (one new prompt).
- Unknown operation or unsafe service name → rejected by the broker allowlist.
- Parent app exits without shutdown → broker detects and exits.

## What is left to the builder

TDD tasks in `docs/superpowers/plans/2026-11-25-windows-service-on-demand-elevation.md`.
