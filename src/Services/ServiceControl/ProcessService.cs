using AppPilot.Domain.Enums;
using AppPilot.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace AppPilot.Services.ServiceControl;

public interface IProcessService
{
    Process? Start(ManagedServiceConfig config);
    Task<bool> StopAsync(ManagedServiceConfig config, int processId, CancellationToken cancellationToken = default);
    bool IsRunning(ManagedServiceConfig config);
    int? GetProcessId(ManagedServiceConfig config);
    ServiceStatus GetStatus(ManagedServiceConfig config);
    string? GetPortOwner(int port);
}

public class ProcessService : IProcessService
{
    private readonly ILogger _logger;
    private readonly Dictionary<string, Process> _runningProcesses = new();

    public ProcessService(ILogger logger)
    {
        _logger = logger;
    }

    public Process? Start(ManagedServiceConfig config)
    {
        try
        {
            if (!System.IO.File.Exists(config.ExecutablePath))
            {
                _logger.Error("Executable not found: {Path}", config.ExecutablePath);
                return null;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = config.ExecutablePath,
                Arguments = config.Arguments,
                WorkingDirectory = string.IsNullOrEmpty(config.WorkingDirectory) 
                    ? System.IO.Path.GetDirectoryName(config.ExecutablePath) 
                    : config.WorkingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            if (config.Environment != null)
            {
                foreach (var env in config.Environment)
                {
                    startInfo.Environment[env.Key] = env.Value;
                }
            }

            var process = Process.Start(startInfo);
            
            if (process != null)
            {
                _runningProcesses[config.Name] = process;
                _logger.Information("Process {Name} started with PID {ProcessId}", config.Name, process.Id);
            }

            return process;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to start process {Name}", config.Name);
            return null;
        }
    }

    public async Task<bool> StopAsync(ManagedServiceConfig config, int processId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var process = Process.GetProcessById(processId);

            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(cancellationToken);
                _logger.Information("Process {Name} (PID: {ProcessId}) stopped", config.Name, processId);
            }

            if (_runningProcesses.Remove(config.Name, out var storedProcess))
            {
                storedProcess?.Dispose();
            }

            return true;
        }
        catch (ArgumentException)
        {
            _logger.Warning("Process with PID {ProcessId} not found", processId);
            if (_runningProcesses.Remove(config.Name, out var storedProcess))
            {
                storedProcess?.Dispose();
            }

            return true;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to stop process {Name}", config.Name);
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
            var processName = System.IO.Path.GetFileNameWithoutExtension(config.ExecutablePath);
            var processes = Process.GetProcessesByName(processName);

            try
            {
                foreach (var process in processes)
                {
                    try
                    {
                        // Match on full executable path to avoid false positives from any other
                        // process that happens to share the same executable name.
                        var mainModulePath = process.MainModule?.FileName;
                        if (string.Equals(mainModulePath, config.ExecutablePath, StringComparison.OrdinalIgnoreCase))
                            return process.Id;
                    }
                    catch
                    {
                        // MainModule access can fail (access denied, process already exited).
                    }
                }
            }
            finally
            {
                foreach (var p in processes) p.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to get process ID for {Name}", config.Name);
        }

        return null;
    }

    public ServiceStatus GetStatus(ManagedServiceConfig config)
    {
        var processId = GetProcessId(config);

        if (!processId.HasValue)
        {
            if (_runningProcesses.Remove(config.Name, out var stale))
                stale?.Dispose();

            return ServiceStatus.Stopped;
        }

        try
        {
            using var process = Process.GetProcessById(processId.Value);
            return process.HasExited ? ServiceStatus.Stopped : ServiceStatus.Running;
        }
        catch
        {
            if (_runningProcesses.Remove(config.Name, out var stale))
                stale?.Dispose();

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
                return null;

            return FindPortOwnerProcess(port);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to check port {Port}", port);
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
                    continue;
                if (!parts[0].Equals("TCP", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!parts[1].EndsWith($":{port}", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!parts[3].Equals("LISTENING", StringComparison.OrdinalIgnoreCase))
                    continue;

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
            _logger.Debug(ex, "Failed to identify process using port {Port}", port);
        }

        return $"Port {port} is already in use by another process.";
    }
}
