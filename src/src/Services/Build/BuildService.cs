using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace AppPilot.Services.Build;

public class BuildService : IBuildService
{
    public async Task<int> LaunchBuildAsync(string projectPath, string displayName = "")
    {
        if (string.IsNullOrWhiteSpace(projectPath))
            return -1;

        var name = string.IsNullOrWhiteSpace(displayName)
            ? Path.GetFileNameWithoutExtension(projectPath)
            : displayName;

        // Single-quote path for PowerShell — only embedded single-quotes need escaping
        var psPath = projectPath.Replace("'", "''");
        var separator = new string('-', Math.Min(name.Length + 12, 56));

        var sb = new StringBuilder();
        sb.AppendLine("$Host.UI.RawUI.WindowTitle = 'Building " + name + "'");
        sb.AppendLine("Clear-Host");
        sb.AppendLine("Write-Host ''");
        sb.AppendLine("Write-Host '  " + separator + "' -ForegroundColor DarkCyan");
        sb.AppendLine("Write-Host \"  Building: " + name + "\" -ForegroundColor Cyan");
        sb.AppendLine("Write-Host '  " + separator + "' -ForegroundColor DarkCyan");
        sb.AppendLine("Write-Host ''");
        sb.AppendLine("dotnet build '" + psPath + "'");
        sb.AppendLine("$code = $LASTEXITCODE");
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
        File.WriteAllText(scriptPath, sb.ToString());

        try
        {
            var shell = IsAvailable("pwsh.exe") ? "pwsh.exe" : "powershell.exe";
            var psi = new ProcessStartInfo(shell, $"-NoLogo -File \"{scriptPath}\"")
            {
                UseShellExecute = true
            };
            using var proc = Process.Start(psi)!;
            await proc.WaitForExitAsync();
            return proc.ExitCode;
        }
        finally
        {
            try { File.Delete(scriptPath); } catch { /* best-effort */ }
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
                RedirectStandardOutput = true
            });
            proc?.WaitForExit();
            return proc?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
