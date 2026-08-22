using AppPilot.Domain.Enums;
using AppPilot.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AppPilot.Services.ServiceControl;

/// <summary>
/// Result of trying to start a managed process.
/// Simple: Success = running, else ErrorMessage for UI + standard log.
/// No per-service file — AppPilot is kill-anytime, so we log only errors via Serilog.
/// </summary>
public class ProcessLaunchResult
{
    /// <summary>Launched process handle, if any.</summary>
    public Process? Process { get; set; }

    /// <summary>True if the process was launched and did not crash quickly.</summary>
    public bool Success { get; set; }

    /// <summary>Human-readable error shown in UI when Success is false.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Exit code if the process exited quickly.</summary>
    public int? ExitCode { get; set; }
}

/// <summary>
/// Controls OS processes for .NET APIs / Worker Services.
/// Simplified: we only log startup errors via standard Serilog (_logger).
/// No per-service files — AppPilot is short-lived (kill-anytime).
/// </summary>
public interface IProcessService
{
    /// <summary>Legacy sync start — delegates to TryStartAsync.</summary>
    Process? Start(ManagedServiceConfig config);

    /// <summary>
    /// Validates, starts, waits briefly for early crash, logs errors to standard logger.
    /// </summary>
    Task<ProcessLaunchResult> TryStartAsync(ManagedServiceConfig config);

    Task<bool> StopAsync(ManagedServiceConfig config, int processId, CancellationToken cancellationToken = default);

    bool IsRunning(ManagedServiceConfig config);

    int? GetProcessId(ManagedServiceConfig config);

    ServiceStatus GetStatus(ManagedServiceConfig config);

    string? GetPortOwner(int port);
}

/// <summary>
/// Simplified fix: validate, start hidden, capture only startup errors, log via _logger (Serilog).
/// No per-service files — AppPilot can be killed anytime, so we keep it simple.
/// Thread safety: singleton, so _runningProcesses is locked on every access.
/// </summary>
public class ProcessService : IProcessService
{
    private readonly ILogger<ProcessService> _logger;

    // Tracks running processes by service name for GetStatus/Stop.
    // WHY LOCK: Dictionary is not thread-safe; UI thread (Start/Stop) and
    // polling timer (GetStatus) access it concurrently.

    private readonly Dictionary<string, Process> _runningProcesses = new();

    // How long to watch for "crashed on startup" — covers missing runtime / bad config.

    private const int EarlyExitWaitMs = 1800;

    public ProcessService(ILogger<ProcessService> logger)
    {
        _logger = logger;
    }

    /// <summary>Sync wrapper for old callers — now just calls TryStartAsync.</summary>
    public Process? Start(ManagedServiceConfig config)
    {
        var result = TryStartAsync(config).GetAwaiter().GetResult();

        return result.Success ? result.Process : null;
    }

    /// <summary>
    /// Validates, starts, and detects early crash.
    /// Simplified: only startup errors are logged via standard Serilog (_logger).
    /// No per-service file — AppPilot is kill-anytime.
    /// Steps:
    /// 1. ValidateConfig — invalid path => fail fast, log error.
    /// 2. Check WorkingDirectory — fail fast, log error.
    /// 3. Start hidden process with stderr/stdout redirected to capture early tail.
    /// 4. Wait 1.8s — if crashed, log tail as error and return ErrorMessage for UI.
    /// </summary>
    public async Task<ProcessLaunchResult> TryStartAsync(ManagedServiceConfig config)
    {
        var validationError = ValidateConfig(config);

        if (validationError != null)
        {
            _logger.LogError("Validation failed for {Name}: {Error}", config.Name, validationError);

            return new ProcessLaunchResult
            {
                Success = false,
                ErrorMessage = validationError
            };
        }

        var workingDir = string.IsNullOrWhiteSpace(config.WorkingDirectory)
            ? Path.GetDirectoryName(config.ExecutablePath) ?? string.Empty
            : config.WorkingDirectory;

        if (!string.IsNullOrWhiteSpace(workingDir) && !Directory.Exists(workingDir))
        {
            var msg = $"Working directory not found: {workingDir}";

            _logger.LogError("{Msg} for {Name}", msg, config.Name);

            return new ProcessLaunchResult
            {
                Success = false,
                ErrorMessage = msg + " - Check WorkingDirectory in service configuration."
            };
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = config.ExecutablePath,
            Arguments = config.Arguments ?? string.Empty,
            WorkingDirectory = string.IsNullOrWhiteSpace(workingDir) ? Path.GetDirectoryName(config.ExecutablePath) ?? Environment.CurrentDirectory : workingDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        if (config.Environment != null)
        {
            foreach (var env in config.Environment)
            {
                startInfo.Environment[env.Key] = env.Value;
            }
        }

        Process? process = null;

        // Capture tail for early-crash diagnosis only — not full lifetime log.
        // WHY LOCK: Output and Error callbacks run on different thread-pool threads.

        var tailLines = new List<string>();

        var tailLock = new object();

        void AddTail(string line)
        {
            lock (tailLock)
            {
                tailLines.Add(line);

                if (tailLines.Count > 100)
                {
                    tailLines.RemoveAt(0);
                }
            }
        }

        try
        {
            process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            process.OutputDataReceived += (s, e) =>
            {
                if (e.Data == null)
                {
                    return;
                }

                AddTail(e.Data);
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data == null)
                {
                    return;
                }

                var line = "[ERR] " + e.Data;

                AddTail(line);
            };

            var started = process.Start();

            if (!started)
            {
                var msg = "Process.Start() returned false - the system failed to launch the executable.";

                _logger.LogError("Failed to start {Name}: {Msg}", config.Name, msg);

                process.Dispose();

                return new ProcessLaunchResult
                {
                    Success = false,
                    ErrorMessage = msg
                };
            }

            process.BeginOutputReadLine();

            process.BeginErrorReadLine();

            lock (_runningProcesses)
            {
                _runningProcesses[config.Name] = process;
            }

            _logger.LogInformation("Process {Name} started with PID {ProcessId}", config.Name, process.Id);

            try
            {
                var exitedQuickly = await WaitForExitWithTimeoutAsync(process, EarlyExitWaitMs);

                if (exitedQuickly)
                {
                    await Task.Delay(300);

                    int exitCode = -1;

                    try
                    {
                        exitCode = process.HasExited ? process.ExitCode : -1;
                    }
                    catch
                    {
                    }

                    string recentTail;

                    lock (tailLock)
                    {
                        recentTail = string.Join(Environment.NewLine, tailLines.TakeLast(Math.Min(30, tailLines.Count)));
                    }

                    string tailForLog = string.IsNullOrWhiteSpace(recentTail) ? "(no output captured)" : recentTail;

                    var friendly = ClassifyFailure(recentTail, exitCode, config);

                    var errorMessage = friendly ?? $"Process '{config.DisplayName}' exited immediately (code {exitCode}).{Environment.NewLine}{tailForLog}";

                    // Only startup errors go to standard Serilog. No per-service file.

                    _logger.LogError("Process {Name} crashed on startup (code {ExitCode}): {Tail}", config.Name, exitCode, tailForLog);

                    if (process.HasExited)
                    {
                        lock (_runningProcesses)
                        {
                            if (_runningProcesses.TryGetValue(config.Name, out var stored) && stored.Id == process.Id)
                            {
                                _runningProcesses.Remove(config.Name);
                            }
                        }
                    }

                    return new ProcessLaunchResult
                    {
                        Success = false,
                        Process = process,
                        ErrorMessage = errorMessage,
                        ExitCode = exitCode
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Early-exit detection failed for {Name}", config.Name);
            }

            return new ProcessLaunchResult
            {
                Success = true,
                Process = process
            };
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            var msg = BuildWin32ErrorMessage(ex, config);

            _logger.LogError(ex, "Failed to start process {Name}", config.Name);

            process?.Dispose();

            return new ProcessLaunchResult
            {
                Success = false,
                ErrorMessage = msg
            };
        }
        catch (Exception ex)
        {
            var msg = $"Failed to start '{config.DisplayName}': {ex.Message}";

            _logger.LogError(ex, "Failed to start process {Name}", config.Name);

            process?.Dispose();

            return new ProcessLaunchResult
            {
                Success = false,
                ErrorMessage = msg
            };
        }
    }

    /// <summary>
    /// Why: invalid path or .dll misconfig is the #1 "window closed instantly" cause.
    /// How: checks empty, bare command (dotnet on PATH), file exists, gives hints (build, extension).
    /// </summary>
    private string? ValidateConfig(ManagedServiceConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ExecutablePath))
        {
            return "Executable path is not configured. Open Service Editor and set ExecutablePath.";
        }

        var exePath = config.ExecutablePath.Trim();

        // Bare command like "dotnet" — found via PATH, skip File.Exists check.

        bool isBareCommand = !Path.IsPathRooted(exePath) && !exePath.Contains(Path.DirectorySeparatorChar) && !exePath.Contains(Path.AltDirectorySeparatorChar) && Path.GetExtension(exePath) == string.Empty;

        if (isBareCommand)
        {
            return null;
        }

        if (!File.Exists(exePath))
        {
            var dir = Path.GetDirectoryName(exePath);

            var hints = new List<string>();

            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                hints.Add($"Directory does not exist: {dir}");
            }

            if (exePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                hints.Add("ExecutablePath points to a .dll - set it to the .exe or use dotnet with dll path");
            }

            if (Path.GetExtension(exePath) == string.Empty)
            {
                hints.Add("No file extension - did you mean .exe or .dll?");
            }

            hints.Add("Build the project (dotnet build) or check the path is relative to BasePath.");

            return $"Executable not found: {exePath}" + (hints.Count > 0 ? Environment.NewLine + string.Join(Environment.NewLine, hints.Select(h => "  * " + h)) : string.Empty);
        }

        if (exePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("ExecutablePath for {Name} points to a .dll - ensure Arguments starts with dotnet or use the .exe", config.Name);
        }

        return null;
    }

    /// <summary>Maps Win32 error codes to actionable messages for UI.</summary>
    private static string BuildWin32ErrorMessage(System.ComponentModel.Win32Exception ex, ManagedServiceConfig config)
    {
        return ex.NativeErrorCode switch
        {
            2 => $"Executable not found: {config.ExecutablePath} (Win32 error 2: file not found). Check the path and build the project.",
            3 => $"Path not found: {config.ExecutablePath} (Win32 error 3). Check WorkingDirectory and BasePath.",
            5 => $"Access denied launching: {config.ExecutablePath} (Win32 error 5). Run AppPilot as Administrator or check file permissions.",
            740 => $"Elevation required for: {config.ExecutablePath} (Win32 error 740). The executable requires administrator privileges.",
            _ => $"Failed to start '{config.DisplayName}': {ex.Message} (Win32 error {ex.NativeErrorCode}). Executable: {config.ExecutablePath}"
        };
    }

    /// <summary>
    /// Turns raw stderr tail into friendly hint for common startup crashes.
    /// Keeps UI error short but actionable, full log stays in file.
    /// </summary>
    private static string? ClassifyFailure(string tail, int exitCode, ManagedServiceConfig config)
    {
        if (string.IsNullOrWhiteSpace(tail))
        {
            return null;
        }

        var lower = tail.ToLowerInvariant();

        if (lower.Contains("address already in use") || lower.Contains("failed to bind") || lower.Contains("eaddrinuse") || (lower.Contains("port") && lower.Contains("already in use")))
        {
            return $"Port conflict - {tail.Trim()}";
        }

        if (lower.Contains("could not load file or assembly") || lower.Contains("filenotfoundexception") || (lower.Contains("missing") && lower.Contains("dll")))
        {
            return $"Missing dependency - {tail.Trim()}{Environment.NewLine}Try: dotnet restore / dotnet build, and check that all NuGet packages are restored.";
        }

        if (lower.Contains("unhandled exception") || lower.Contains("exception"))
        {
            return $"Application crashed on startup (exit code {exitCode}):{Environment.NewLine}{tail.Trim()}{Environment.NewLine}Full log: see Logs/Services folder.";
        }

        if (lower.Contains("unable to start kestrel") || lower.Contains("microsoft.aspnetcore"))
        {
            return $"ASP.NET startup failure (exit code {exitCode}):{Environment.NewLine}{tail.Trim()}";
        }

        if (lower.Contains("it was not possible to find any compatible framework"))
        {
            return $".NET runtime not found (exit code {exitCode}):{Environment.NewLine}{tail.Trim()}{Environment.NewLine}Install the required .NET runtime/SDK.";
        }

        return null;
    }

    /// <summary>
    /// Waits up to timeoutMs for early crash. Returns true if process exited quickly
    /// (means startup failure, not normal run).
    /// </summary>
    private static async Task<bool> WaitForExitWithTimeoutAsync(Process process, int timeoutMs)
    {
        try
        {
            var tcs = new TaskCompletionSource<bool>();

            void Handler(object? s, EventArgs e) => tcs.TrySetResult(true);

            process.Exited += Handler;

            try
            {
                if (process.HasExited)
                {
                    return true;
                }

                var delay = Task.Delay(timeoutMs);

                var completed = await Task.WhenAny(tcs.Task, delay);

                return completed == tcs.Task;
            }
            finally
            {
                process.Exited -= Handler;
            }
        }
        catch
        {
            return process.HasExited;
        }
    }

    /// <summary>
    /// Stops a running process. Logs only via standard Serilog.
    /// No per-service file — simple kill and cleanup.
    /// </summary>
    public async Task<bool> StopAsync(ManagedServiceConfig config, int processId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var process = Process.GetProcessById(processId);

            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);

                await process.WaitForExitAsync(cancellationToken);

                _logger.LogInformation("Process {Name} (PID: {ProcessId}) stopped", config.Name, processId);
            }

            lock (_runningProcesses)
            {
                if (_runningProcesses.Remove(config.Name, out var storedProcess))
                {
                    storedProcess?.Dispose();
                }
            }

            return true;
        }
        catch (ArgumentException)
        {
            _logger.LogWarning("Process with PID {ProcessId} not found", processId);

            lock (_runningProcesses)
            {
                if (_runningProcesses.Remove(config.Name, out var storedProcess))
                {
                    storedProcess?.Dispose();
                }
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop process {Name}", config.Name);

            throw new InvalidOperationException($"Failed to stop '{config.DisplayName}': {ex.Message}", ex);
        }
    }

    public bool IsRunning(ManagedServiceConfig config)
    {
        var processId = GetProcessId(config);
        if (processId.HasValue)
        {
            try
            {
                using var process = Process.GetProcessById(processId.Value);
                return !process.HasExited;
            }
            catch
            {
                return false;
            }
        }
        return false;
    }

    public int? GetProcessId(ManagedServiceConfig config)
    {
        try
        {
            lock (_runningProcesses)
            {
                if (_runningProcesses.TryGetValue(config.Name, out var tracked) && !tracked.HasExited)
                {
                    return tracked.Id;
                }
            }

            var processName = Path.GetFileNameWithoutExtension(config.ExecutablePath);

            if (string.IsNullOrWhiteSpace(processName))
            {
                return null;
            }

            var processes = Process.GetProcessesByName(processName);

            int? foundId = null;

            try
            {
                foreach (var process in processes)
                {
                    try
                    {
                        var mainModulePath = process.MainModule?.FileName;

                        if (!string.IsNullOrEmpty(mainModulePath)
                            && string.Equals(mainModulePath, config.ExecutablePath, StringComparison.OrdinalIgnoreCase)
                            && !process.HasExited)
                        {
                            foundId = process.Id;
                        }
                    }
                    catch
                    {
                    }
                }
            }
            finally
            {
                foreach (var p in processes)
                {
                    p.Dispose();
                }
            }

            return foundId;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get process ID for {Name}", config.Name);
        }

        return null;
    }

    public ServiceStatus GetStatus(ManagedServiceConfig config)
    {
        var processId = GetProcessId(config);

        if (!processId.HasValue)
        {
            lock (_runningProcesses)
            {
                if (_runningProcesses.Remove(config.Name, out var stale))
                {
                    stale?.Dispose();
                }
            }

            return ServiceStatus.Stopped;
        }

        try
        {
            using var process = Process.GetProcessById(processId.Value);

            if (process.HasExited)
            {
                return ServiceStatus.Stopped;
            }

            lock (_runningProcesses)
            {
                if (_runningProcesses.TryGetValue(config.Name, out var tracked) && tracked.Id == processId.Value && !tracked.HasExited)
                {
                    return ServiceStatus.Running;
                }
            }

            string mainModulePath = process.MainModule?.FileName;

            if (!string.IsNullOrEmpty(mainModulePath) && string.Equals(mainModulePath, config.ExecutablePath, StringComparison.OrdinalIgnoreCase))
            {
                return ServiceStatus.Running;
            }

            return ServiceStatus.Stopped;
        }
        catch
        {
            lock (_runningProcesses)
            {
                if (_runningProcesses.Remove(config.Name, out var stale))
                {
                    stale?.Dispose();
                }
            }

            return ServiceStatus.Stopped;
        }
    }

    public string? GetPortOwner(int port)
    {
        try
        {
            var isInUse = IPGlobalProperties.GetIPGlobalProperties()
                .GetActiveTcpListeners()
                .Any(ep => ep.Port == port);

            if (!isInUse)
            {
                return null;
            }

            return FindPortOwnerProcess(port);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check port {Port}", port);
            return null; // Don't block the start attempt if the check itself fails
        }
    }

    private string FindPortOwnerProcess(int port)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netstat",
                Arguments = "-ano",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null)
                return $"Port {port} is already in use by another process.";

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(3000);

            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                // netstat -ano TCP line: [TCP] [LocalAddr:Port] [ForeignAddr] [State] [PID]
                if (parts.Length < 5)
                {
                    continue;
                }

                if (!parts[0].Equals("TCP", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!parts[1].EndsWith($":{port}", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!parts[3].Equals("LISTENING", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (int.TryParse(parts[^1], out var pid))
                {
                    try
                    {
                        var owner = Process.GetProcessById(pid);
                        return $"Port {port} is already in use by '{owner.ProcessName}' (PID {pid}).";
                    }
                    catch
                    {
                        return $"Port {port} is already in use by PID {pid}.";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to identify process using port {Port}", port);
        }

        return $"Port {port} is already in use by another process.";
    }
}
