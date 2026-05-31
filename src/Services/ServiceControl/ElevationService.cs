using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
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
/// Provides elevation services for running commands that require administrator privileges.
/// Uses the industry-standard approach of UAC elevation via the "runas" verb.
/// </summary>
public interface IElevationService
{
    /// <summary>
    /// Checks if the current process is running with administrator privileges.
    /// </summary>
    bool IsElevated { get; }

    /// <summary>
    /// Runs a command with administrator privileges using UAC elevation.
    /// If the user cancels the UAC prompt, returns a cancelled result.
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
    /// Runs a command without elevation. Uses ShellExecute for consistency.
    /// </summary>
    Task<ElevatedCommandResult> RunCommandAsync(
        string fileName,
        string arguments,
        string operationDescription,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of IElevationService using Windows UAC (User Account Control).
/// </summary>
public class ElevationService : IElevationService
{
    private readonly ILogger<ElevationService> _logger;
    private readonly Lazy<bool> _isElevated;

    private const int CommandTimeoutSeconds = 60;

    public ElevationService(ILogger<ElevationService> logger)
    {
        _logger = logger;
        _isElevated = new Lazy<bool>(CheckIsElevated);
    }

    /// <inheritdoc />
    public bool IsElevated => _isElevated.Value;

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
        _logger.LogInformation(
            "Running elevated command: {Operation} - {FileName} {Arguments}",
            operationDescription, fileName, arguments);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                // UseShellExecute must be true for the "runas" verb to work
                UseShellExecute = true,
                // Request elevation via UAC
                Verb = "runas",
                // Don't create a visible window for console apps
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };

            try
            {
                if (!process.Start())
                {
                    _logger.LogError("Failed to start elevated process for {Operation}", operationDescription);
                    return new ElevatedCommandResult
                    {
                        Success = false,
                        ExitCode = -1,
                        ErrorMessage = "Failed to start the elevated process."
                    };
                }
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) // ERROR_CANCELLED
            {
                // User declined the UAC prompt
                _logger.LogWarning("User cancelled UAC prompt for {Operation}", operationDescription);
                return new ElevatedCommandResult
                {
                    Success = false,
                    ExitCode = -1,
                    ErrorMessage = "Administrator permission is required for this operation. The UAC prompt was cancelled.",
                    WasCancelled = true
                };
            }

            // Wait for the process to exit with timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(CommandTimeoutSeconds));

            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Timeout occurred
                try { process.Kill(entireProcessTree: true); } catch { /* Ignore */ }
                _logger.LogError("Elevated command timed out for {Operation}", operationDescription);
                return new ElevatedCommandResult
                {
                    Success = false,
                    ExitCode = -1,
                    ErrorMessage = $"The operation timed out after {CommandTimeoutSeconds} seconds."
                };
            }

            if (process.ExitCode == 0)
            {
                _logger.LogInformation("Elevated command completed successfully: {Operation}", operationDescription);
                return new ElevatedCommandResult
                {
                    Success = true,
                    ExitCode = 0
                };
            }
            else
            {
                // Note: With UseShellExecute=true and Verb="runas", we can't capture stdout/stderr
                // The exit code is the primary indicator of success/failure
                _logger.LogWarning(
                    "Elevated command exited with code {ExitCode}: {Operation}",
                    process.ExitCode, operationDescription);
                return new ElevatedCommandResult
                {
                    Success = false,
                    ExitCode = process.ExitCode,
                    ErrorMessage = $"The operation failed with exit code {process.ExitCode}."
                };
            }
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // User declined the UAC prompt
            _logger.LogWarning("User cancelled UAC prompt for {Operation}", operationDescription);
            return new ElevatedCommandResult
            {
                Success = false,
                ExitCode = -1,
                ErrorMessage = "Administrator permission is required for this operation. The UAC prompt was cancelled.",
                WasCancelled = true
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception running elevated command: {Operation}", operationDescription);
            return new ElevatedCommandResult
            {
                Success = false,
                ExitCode = -1,
                ErrorMessage = $"Failed to execute the operation: {ex.Message}"
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
        _logger.LogDebug(
            "Running command: {Operation} - {FileName} {Arguments}",
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
            _logger.LogError(ex, "Exception running command: {Operation}", operationDescription);
            return new ElevatedCommandResult
            {
                Success = false,
                ExitCode = -1,
                ErrorMessage = $"Failed to execute the operation: {ex.Message}"
            };
        }
    }
}
