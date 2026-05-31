using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AppPilot.Services.ServiceControl;

/// <summary>
/// Represents the result of an elevated command execution.
/// </summary>
public class ElevatedCommandResult
{
    /// <summary>
    /// Indicates whether the command executed successfully (exit code 0).
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The process exit code, or -1 if the process could not be started.
    /// </summary>
    public int ExitCode { get; init; }

    /// <summary>
    /// Error message if the command failed, otherwise empty.
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// Indicates whether the user cancelled the UAC prompt.
    /// </summary>
    public bool WasCancelled { get; init; }
}

/// <summary>
/// Command sent to the elevated helper process.
/// </summary>
internal class ElevatedCommand
{
    public string FileName { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Response from the elevated helper process.
/// </summary>
internal class ElevatedResponse
{
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public string Output { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}

/// <summary>
/// Provides elevation services for running commands that require administrator privileges.
/// Uses the industry-standard approach of a persistent elevated helper process with named pipe IPC.
/// UAC is prompted only once per application session.
/// </summary>
public interface IElevationService : IDisposable
{
    /// <summary>
    /// Checks if the current process is running with administrator privileges.
    /// </summary>
    bool IsElevated { get; }

    /// <summary>
    /// Indicates whether an elevated helper is currently available.
    /// </summary>
    bool HasElevatedHelper { get; }

    /// <summary>
    /// Runs a command with administrator privileges.
    /// On first call, prompts for UAC to start the elevated helper.
    /// Subsequent calls reuse the existing elevated helper (no additional UAC prompts).
    /// </summary>
    /// <param name="fileName">The program to execute (e.g., "sc.exe").</param>
    /// <param name="arguments">Command-line arguments.</param>
    /// <param name="operationDescription">Human-readable description for logging.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing success status, exit code, and any error message.</returns>
    Task<ElevatedCommandResult> RunElevatedAsync(
        string fileName,
        string arguments,
        string operationDescription,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a command without elevation.
    /// </summary>
    Task<ElevatedCommandResult> RunCommandAsync(
        string fileName,
        string arguments,
        string operationDescription,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Shuts down the elevated helper process if running.
    /// </summary>
    void Shutdown();
}

/// <summary>
/// Implementation of IElevationService using a persistent elevated helper process.
/// The helper is spawned once with UAC elevation and reused for all subsequent operations.
/// </summary>
public class ElevationService : IElevationService
{
    private readonly ILogger<ElevationService> _logger;
    private readonly Lazy<bool> _isElevated;
    private readonly SemaphoreSlim _helperLock = new(1, 1);
    private readonly object _pipeLock = new();
    
    private Process? _helperProcess;
    private NamedPipeClientStream? _pipeClient;
    private StreamReader? _pipeReader;
    private StreamWriter? _pipeWriter;
    private bool _disposed;

    private const int CommandTimeoutSeconds = 60;
    private const int ConnectionTimeoutMs = 10000;
    
    /// <summary>
    /// The pipe name used for IPC between the main app and elevated helper.
    /// Includes process ID of the main app to ensure uniqueness.
    /// </summary>
    internal static string GetPipeName(int mainProcessId) => $"AppPilot_ElevatedHelper_{mainProcessId}";

    /// <summary>
    /// Command-line argument that indicates the process should run as an elevated helper.
    /// </summary>
    public const string HelperModeArgument = "--elevated-helper";

    public ElevationService(ILogger<ElevationService> logger)
    {
        _logger = logger;
        _isElevated = new Lazy<bool>(CheckIsElevated);
    }

    /// <inheritdoc />
    public bool IsElevated => _isElevated.Value;

    /// <inheritdoc />
    public bool HasElevatedHelper => _helperProcess != null && !_helperProcess.HasExited;

    private static bool CheckIsElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<ElevatedCommandResult> RunElevatedAsync(
        string fileName,
        string arguments,
        string operationDescription,
        CancellationToken cancellationToken = default)
    {
        // If already running as admin, execute directly
        if (IsElevated)
        {
            return await RunCommandAsync(fileName, arguments, operationDescription, cancellationToken);
        }

        await _helperLock.WaitAsync(cancellationToken);
        try
        {
            // Ensure helper is running
            if (!await EnsureHelperRunningAsync(cancellationToken))
            {
                return new ElevatedCommandResult
                {
                    Success = false,
                    ExitCode = -1,
                    ErrorMessage = "Administrator permission is required for this operation. The UAC prompt was cancelled.",
                    WasCancelled = true
                };
            }

            // Send command to helper
            return await SendCommandToHelperAsync(fileName, arguments, operationDescription, cancellationToken);
        }
        finally
        {
            _helperLock.Release();
        }
    }

    private async Task<bool> EnsureHelperRunningAsync(CancellationToken cancellationToken)
    {
        // Check if helper is already running and responsive
        if (_helperProcess != null && !_helperProcess.HasExited && _pipeClient?.IsConnected == true)
        {
            return true;
        }

        // Clean up any stale connection
        CleanupHelper();

        _logger.LogInformation("Starting elevated helper process (UAC prompt will appear)");

        try
        {
            var currentProcessId = Environment.ProcessId;
            var pipeName = GetPipeName(currentProcessId);
            var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;

            if (string.IsNullOrEmpty(exePath))
            {
                _logger.LogError("Could not determine executable path for elevated helper");
                return false;
            }

            // Start elevated helper process
            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"{HelperModeArgument} {currentProcessId}",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };

            try
            {
                _helperProcess = Process.Start(startInfo);
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                _logger.LogWarning("User cancelled UAC prompt for elevated helper");
                return false;
            }

            if (_helperProcess == null)
            {
                _logger.LogError("Failed to start elevated helper process");
                return false;
            }

            _logger.LogInformation("Elevated helper started with PID {ProcessId}", _helperProcess.Id);

            // Connect to the helper via named pipe
            _pipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

            try
            {
                await _pipeClient.ConnectAsync(ConnectionTimeoutMs, cancellationToken);
            }
            catch (TimeoutException)
            {
                _logger.LogError("Timeout connecting to elevated helper");
                CleanupHelper();
                return false;
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Failed to connect to elevated helper pipe");
                CleanupHelper();
                return false;
            }

            _pipeReader = new StreamReader(_pipeClient, Encoding.UTF8, leaveOpen: true);
            _pipeWriter = new StreamWriter(_pipeClient, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

            _logger.LogInformation("Connected to elevated helper");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start elevated helper");
            CleanupHelper();
            return false;
        }
    }

    private async Task<ElevatedCommandResult> SendCommandToHelperAsync(
        string fileName,
        string arguments,
        string description,
        CancellationToken cancellationToken)
    {
        if (_pipeWriter == null || _pipeReader == null)
        {
            return new ElevatedCommandResult
            {
                Success = false,
                ExitCode = -1,
                ErrorMessage = "Not connected to elevated helper."
            };
        }

        try
        {
            _logger.LogDebug("Sending command to elevated helper: {Description}", description);

            var command = new ElevatedCommand
            {
                FileName = fileName,
                Arguments = arguments,
                Description = description
            };

            var commandJson = JsonSerializer.Serialize(command);

            lock (_pipeLock)
            {
                _pipeWriter.WriteLine(commandJson);
            }

            // Read response with timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(CommandTimeoutSeconds));

            string? responseJson;
            lock (_pipeLock)
            {
                responseJson = _pipeReader.ReadLine();
            }

            if (string.IsNullOrEmpty(responseJson))
            {
                _logger.LogError("Empty response from elevated helper");
                CleanupHelper();
                return new ElevatedCommandResult
                {
                    Success = false,
                    ExitCode = -1,
                    ErrorMessage = "No response from elevated helper. It may have crashed."
                };
            }

            var response = JsonSerializer.Deserialize<ElevatedResponse>(responseJson);
            if (response == null)
            {
                return new ElevatedCommandResult
                {
                    Success = false,
                    ExitCode = -1,
                    ErrorMessage = "Invalid response from elevated helper."
                };
            }

            if (response.Success)
            {
                _logger.LogInformation("Elevated command completed successfully: {Description}", description);
            }
            else
            {
                _logger.LogWarning("Elevated command failed: {Description} - {Error}", description, response.Error);
            }

            return new ElevatedCommandResult
            {
                Success = response.Success,
                ExitCode = response.ExitCode,
                ErrorMessage = response.Error
            };
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Pipe communication error with elevated helper");
            CleanupHelper();
            return new ElevatedCommandResult
            {
                Success = false,
                ExitCode = -1,
                ErrorMessage = "Communication with elevated helper failed. Please try again."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending command to elevated helper");
            return new ElevatedCommandResult
            {
                Success = false,
                ExitCode = -1,
                ErrorMessage = $"Failed to execute command: {ex.Message}"
            };
        }
    }

    /// <inheritdoc />
    public async Task<ElevatedCommandResult> RunCommandAsync(
        string fileName,
        string arguments,
        string operationDescription,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Running command: {Operation} - {FileName} {Arguments}",
            operationDescription, fileName, arguments);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return new ElevatedCommandResult
                {
                    Success = false,
                    ExitCode = -1,
                    ErrorMessage = "Failed to start the process."
                };
            }

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(CommandTimeoutSeconds));

            await process.WaitForExitAsync(cts.Token);
            var output = await outputTask;
            var error = await errorTask;

            if (process.ExitCode == 0)
            {
                return new ElevatedCommandResult
                {
                    Success = true,
                    ExitCode = 0
                };
            }
            else
            {
                var errorMessage = !string.IsNullOrWhiteSpace(error) ? error.Trim()
                    : !string.IsNullOrWhiteSpace(output) ? output.Trim()
                    : $"The operation failed with exit code {process.ExitCode}.";

                return new ElevatedCommandResult
                {
                    Success = false,
                    ExitCode = process.ExitCode,
                    ErrorMessage = errorMessage
                };
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception running command: {operationDescription}", operationDescription);
            return new ElevatedCommandResult
            {
                Success = false,
                ExitCode = -1,
                ErrorMessage = $"Failed to execute the operation: {ex.Message}"
            };
        }
    }

    /// <inheritdoc />
    public void Shutdown()
    {
        _logger.LogInformation("Shutting down elevation service");
        CleanupHelper();
    }

    private void CleanupHelper()
    {
        try
        {
            _pipeWriter?.Dispose();
            _pipeReader?.Dispose();
            _pipeClient?.Dispose();

            if (_helperProcess != null && !_helperProcess.HasExited)
            {
                try
                {
                    _helperProcess.Kill();
                }
                catch { /* Ignore - process may have already exited */ }
            }

            _helperProcess?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error cleaning up elevated helper");
        }
        finally
        {
            _pipeWriter = null;
            _pipeReader = null;
            _pipeClient = null;
            _helperProcess = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Shutdown();
        _helperLock.Dispose();
    }
}

/// <summary>
/// The elevated helper that runs as an administrator and executes commands received via named pipe.
/// </summary>
public static class ElevatedHelper
{
    /// <summary>
    /// Runs the elevated helper mode. This method blocks until the parent process exits or sends a shutdown signal.
    /// </summary>
    /// <param name="parentProcessId">The process ID of the parent (main) AppPilot process.</param>
    public static void Run(int parentProcessId)
    {
        var pipeName = ElevationService.GetPipeName(parentProcessId);

        // Set up pipe security to only allow the current user
        var pipeSecurity = new PipeSecurity();
        var currentUser = WindowsIdentity.GetCurrent().User;
        if (currentUser != null)
        {
            pipeSecurity.AddAccessRule(new PipeAccessRule(
                currentUser,
                PipeAccessRights.FullControl,
                AccessControlType.Allow));
        }

        using var pipeServer = NamedPipeServerStreamAcl.Create(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            0, 0,
            pipeSecurity);

        Console.WriteLine($"[ElevatedHelper] Waiting for connection on pipe: {pipeName}");

        // Wait for connection with timeout
        var connectTask = pipeServer.WaitForConnectionAsync();
        if (!connectTask.Wait(TimeSpan.FromSeconds(30)))
        {
            Console.WriteLine("[ElevatedHelper] Connection timeout, exiting");
            return;
        }

        Console.WriteLine("[ElevatedHelper] Client connected");

        using var reader = new StreamReader(pipeServer, Encoding.UTF8, leaveOpen: true);
        using var writer = new StreamWriter(pipeServer, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

        // Monitor parent process - exit if parent dies
        Process parentProcess;
        try
        {
            parentProcess = Process.GetProcessById(parentProcessId);
        }
        catch
        {
            Console.WriteLine("[ElevatedHelper] Parent process not found, exiting");
            return;
        }

        while (!parentProcess.HasExited && pipeServer.IsConnected)
        {
            try
            {
                var commandJson = reader.ReadLine();
                if (string.IsNullOrEmpty(commandJson))
                {
                    // Pipe closed or empty line
                    if (!pipeServer.IsConnected) break;
                    continue;
                }

                // Check for shutdown command
                if (commandJson == "SHUTDOWN")
                {
                    Console.WriteLine("[ElevatedHelper] Shutdown command received");
                    break;
                }

                var command = JsonSerializer.Deserialize<ElevatedCommand>(commandJson);
                if (command == null)
                {
                    writer.WriteLine(JsonSerializer.Serialize(new ElevatedResponse
                    {
                        Success = false,
                        ExitCode = -1,
                        Error = "Invalid command format"
                    }));
                    continue;
                }

                Console.WriteLine($"[ElevatedHelper] Executing: {command.FileName} {command.Arguments}");

                // Execute the command
                var response = ExecuteCommand(command);
                writer.WriteLine(JsonSerializer.Serialize(response));
            }
            catch (IOException)
            {
                // Pipe disconnected
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ElevatedHelper] Error: {ex.Message}");
                try
                {
                    writer.WriteLine(JsonSerializer.Serialize(new ElevatedResponse
                    {
                        Success = false,
                        ExitCode = -1,
                        Error = ex.Message
                    }));
                }
                catch { /* Ignore write errors */ }
            }
        }

        Console.WriteLine("[ElevatedHelper] Shutting down");
    }

    private static ElevatedResponse ExecuteCommand(ElevatedCommand command)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = command.FileName,
                Arguments = command.Arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return new ElevatedResponse
                {
                    Success = false,
                    ExitCode = -1,
                    Error = "Failed to start process"
                };
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(60000); // 60 second timeout

            if (!process.HasExited)
            {
                try { process.Kill(); } catch { }
                return new ElevatedResponse
                {
                    Success = false,
                    ExitCode = -1,
                    Error = "Command timed out after 60 seconds"
                };
            }

            return new ElevatedResponse
            {
                Success = process.ExitCode == 0,
                ExitCode = process.ExitCode,
                Output = output,
                Error = process.ExitCode != 0
                    ? (!string.IsNullOrWhiteSpace(error) ? error.Trim()
                        : !string.IsNullOrWhiteSpace(output) ? output.Trim()
                        : $"Command failed with exit code {process.ExitCode}")
                    : string.Empty
            };
        }
        catch (Exception ex)
        {
            return new ElevatedResponse
            {
                Success = false,
                ExitCode = -1,
                Error = ex.Message
            };
        }
    }
}
