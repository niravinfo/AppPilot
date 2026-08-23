using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AppPilot.Services.Build;

/// <summary>
/// Builds .NET projects in a visible terminal.
/// Fix for "instant close": validates path, checks dotnet in elevated context,
/// uses Bypass to avoid Restricted policy, and keeps window open on ANY error
/// (dotnet missing, build failed, shell missing) via ReadKey. Logs only startup
/// errors to standard Serilog — AppPilot is kill-anytime.
/// </summary>
public class BuildService : IBuildService
{
    private readonly ILogger<BuildService> _logger;

    public BuildService(ILogger<BuildService> logger)
    {
        _logger = logger;
    }

    public async Task<int> LaunchBuildAsync(string projectPath, string displayName = "")
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            _logger.LogError("Build path is empty");

            return -1;
        }

        if (!File.Exists(projectPath) && !Directory.Exists(projectPath))
        {
            _logger.LogError("Build file not found: {Path}", projectPath);

            return -1;
        }

        var name = string.IsNullOrWhiteSpace(displayName)
            ? Path.GetFileNameWithoutExtension(projectPath)
            : displayName;

        // Single-quote path for PowerShell — only embedded single-quotes need escaping.

        var psPath = projectPath.Replace("'", "''");

        var separator = new string('-', Math.Min(name.Length + 12, 56));

        // Check dotnet in this (possibly elevated) context. If missing here,
        // the PS window would close after 2s because $LASTEXITCODE stays 0.

        if (!IsAvailable("dotnet"))
        {
            _logger.LogError("dotnet not found in PATH (elevated context). Try where.exe dotnet or use full path to dotnet.exe");
        }

        var sb = new StringBuilder();

        sb.AppendLine("$Host.UI.RawUI.WindowTitle = 'Building " + name.Replace("'", "''") + "'");

        sb.AppendLine("Clear-Host");

        sb.AppendLine("Write-Host ''");

        sb.AppendLine("Write-Host '  " + separator + "' -ForegroundColor DarkCyan");

        sb.AppendLine("Write-Host \"  Building: " + name.Replace("\"", "`\"") + "\" -ForegroundColor Cyan");

        sb.AppendLine("Write-Host '  " + separator + "' -ForegroundColor DarkCyan");

        sb.AppendLine("Write-Host ''");

        // Keep window open on ANY error, including dotnet not found (CommandNotFound).

        sb.AppendLine("try { dotnet build '" + psPath + "' } catch { Write-Host $_.Exception.Message -ForegroundColor Red; $global:LASTEXITCODE = 1 }");

        sb.AppendLine("$code = $LASTEXITCODE");

        // If dotnet was not found, LASTEXITCODE can still be 0 — also check $?.

        sb.AppendLine("if (-not $? ) { $code = 1 }");

        sb.AppendLine("Write-Host ''");

        sb.AppendLine("if ($code -eq 0) {");

        sb.AppendLine("    Write-Host '  Build SUCCEEDED' -ForegroundColor Green");

        sb.AppendLine("    Write-Host ''");

        sb.AppendLine("    Start-Sleep -Seconds 2");

        sb.AppendLine("    exit 0");

        sb.AppendLine("} else {");

        sb.AppendLine("    Write-Host \"  Build FAILED  (exit code: $code)\" -ForegroundColor Red");

        sb.AppendLine("    Write-Host ''");

        sb.AppendLine("    Write-Host '  Press any key to close...' -ForegroundColor Yellow");

        sb.AppendLine("    $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')");

        sb.AppendLine("    exit 1");

        sb.AppendLine("}");

        var scriptPath = Path.Combine(Path.GetTempPath(), $"apppilot_build_{Guid.NewGuid():N}.ps1");

        File.WriteAllText(scriptPath, sb.ToString(), Encoding.UTF8);

        try
        {
            // Choose shell with Bypass to survive Restricted policy. Log choice.

            var shell = IsAvailable("pwsh.exe") ? "pwsh.exe" : "powershell.exe";

            _logger.LogInformation("Launching build for {Name} via {Shell}: {Path}", name, shell, projectPath);

            var psi = new ProcessStartInfo(shell, $"-NoLogo -ExecutionPolicy Bypass -File \"{scriptPath}\"")
            {
                UseShellExecute = true
            };

            using var proc = Process.Start(psi);

            if (proc == null)
            {
                _logger.LogError("Failed to start shell {Shell} for build {Name}", shell, name);

                return 1;
            }

            await proc.WaitForExitAsync();

            if (proc.ExitCode != 0)
            {
                _logger.LogError("Build failed for {Name} (exit {Code}): {Path}", name, proc.ExitCode, projectPath);
            }

            return proc.ExitCode;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            _logger.LogError(ex, "Failed to launch build terminal for {Name}. Shell missing or blocked", name);

            return 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error launching build for {Name}", name);

            return 1;
        }
        finally
        {
            try
            {
                File.Delete(scriptPath);
            }
            catch
            {
            }
        }
    }

    private static bool IsAvailable(string executable)
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo(executable, "--version")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            if (proc == null)
            {
                return false;
            }

            proc.WaitForExit(2000);

            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
