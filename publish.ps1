<#
.SYNOPSIS
    Build, package, and optionally publish AppPilot to GitHub Releases using Velopack.

.PARAMETER Version
    The semantic version to publish, e.g. "1.2.0". Defaults to the version in AppPilot.csproj.

.PARAMETER GitHubRepo
    The GitHub repository in "owner/repo" form, e.g. "YourUsername/AppPilot".
    Required only when -Publish is specified.

.PARAMETER GitHubToken
    A GitHub personal access token with "repo" scope.
    Can also be set via the GITHUB_TOKEN environment variable.
    Required only when -Publish is specified.

.PARAMETER Publish
    When set, uploads the release package to GitHub Releases after packaging.

.PARAMETER Channel
    Velopack release channel (default: "stable"). Use "beta" or "preview" for pre-releases.

.EXAMPLE
    # Local build & package only
    .\publish.ps1 -Version 1.2.0

.EXAMPLE
    # Build, package, and push to GitHub Releases
    .\publish.ps1 -Version 1.2.0 -GitHubRepo "YourUsername/AppPilot" -Publish

.EXAMPLE
    # Using environment variable for token
    $env:GITHUB_TOKEN = "ghp_..."
    .\publish.ps1 -Version 1.2.0 -GitHubRepo "YourUsername/AppPilot" -Publish
#>

[CmdletBinding()]
param(
    [string] $Version,
    [string] $GitHubRepo,
    [string] $GitHubToken = $env:GITHUB_TOKEN,
    [switch] $Publish,
    [string] $Channel = "stable"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------
# Paths
# ---------------------------------------------------------------------------
$RepoRoot    = $PSScriptRoot
$ProjectDir  = Join-Path $RepoRoot "src"
$ProjectFile = Join-Path $ProjectDir "AppPilot.csproj"
$PublishDir  = Join-Path $RepoRoot "publish\app"
$ReleasesDir = Join-Path $RepoRoot "publish\releases"
$MainExe     = "AppPilot.exe"

# ---------------------------------------------------------------------------
# Resolve version (from .csproj if not supplied)
# ---------------------------------------------------------------------------
if (-not $Version) {
    $xml = [xml](Get-Content $ProjectFile)
    $Version = $xml.Project.PropertyGroup.Version | Select-Object -First 1
    if (-not $Version) { throw "Could not read <Version> from $ProjectFile. Pass -Version explicitly." }
}

Write-Host ""
Write-Host "  ========================================" -ForegroundColor DarkCyan
Write-Host "  AppPilot publish  v$Version" -ForegroundColor Cyan
Write-Host "  ========================================" -ForegroundColor DarkCyan
Write-Host ""

# ---------------------------------------------------------------------------
# Ensure vpk (Velopack CLI) is installed
# ---------------------------------------------------------------------------
if (-not (Get-Command vpk -ErrorAction SilentlyContinue)) {
    Write-Host "  Installing Velopack CLI (vpk)..." -ForegroundColor Yellow
    dotnet tool install -g vpk
    # Refresh PATH so vpk is found in this session
    $env:PATH = [System.Environment]::GetEnvironmentVariable("PATH", "User") + ";" + $env:PATH
}

# vpk targets .NET 9; allow it to run on any newer major version (e.g. .NET 10)
$env:DOTNET_ROLL_FORWARD = "Major"

# ---------------------------------------------------------------------------
# 1. dotnet publish
# ---------------------------------------------------------------------------
Write-Host "  [1/3] Publishing project..." -ForegroundColor Cyan
if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }

dotnet publish $ProjectFile `
    --configuration Release `
    --runtime win-x64 `
    --self-contained `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    --output $PublishDir

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit code $LASTEXITCODE)" }

# ---------------------------------------------------------------------------
# 2. vpk pack — create the Velopack installer / release package
# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "  [2/3] Packaging with Velopack..." -ForegroundColor Cyan
if (Test-Path $ReleasesDir) { Remove-Item $ReleasesDir -Recurse -Force }
New-Item -ItemType Directory -Path $ReleasesDir | Out-Null

vpk pack `
    --packId    "AppPilot" `
    --packTitle "AppPilot" `
    --packVersion $Version `
    --packDir   $PublishDir `
    --mainExe   $MainExe `
    --channel   $Channel `
    --outputDir $ReleasesDir

if ($LASTEXITCODE -ne 0) { throw "vpk pack failed (exit code $LASTEXITCODE)" }

# ---------------------------------------------------------------------------
# 3. (Optional) Upload to GitHub Releases
# ---------------------------------------------------------------------------
if ($Publish) {
    if (-not $GitHubRepo)  { throw "-GitHubRepo is required when using -Publish (e.g. 'YourUsername/AppPilot')" }
    if (-not $GitHubToken) { throw "-GitHubToken or `$env:GITHUB_TOKEN is required when using -Publish" }

    Write-Host ""
    Write-Host "  [3/3] Uploading to GitHub Releases ($GitHubRepo)..." -ForegroundColor Cyan

    vpk upload github `
        --repoUrl    "https://github.com/$GitHubRepo" `
        --publish `
        --releaseName "v$Version" `
        --tag         "v$Version" `
        --token       $GitHubToken `
        --channel     $Channel `
        --outputDir   $ReleasesDir

    if ($LASTEXITCODE -ne 0) { throw "vpk upload failed (exit code $LASTEXITCODE)" }

    Write-Host ""
    Write-Host "  Release v$Version published to https://github.com/$GitHubRepo/releases" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "  [3/3] Skipping GitHub upload (pass -Publish to upload)" -ForegroundColor DarkGray
}

# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "  Done! Packages are in: $ReleasesDir" -ForegroundColor Green
Write-Host ""
