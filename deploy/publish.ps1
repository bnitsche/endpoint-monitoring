#Requires -Version 5.1
<#
.SYNOPSIS
    Publishes the Endpoint Monitoring Web app and Monitoring Service and packs
    both, together with the deployment script, into a single .zip package.

.DESCRIPTION
    Run this ON YOUR DEV / BUILD MACHINE from anywhere in the repo. It:
      1. dotnet publish EndpointMonitoring.Web      -> <OutputDir>\web
      2. dotnet publish EndpointMonitoring.Service  -> <OutputDir>\service
      3. Zips web\, service\, deploy.ps1 and DEPLOYMENT.md into
         <OutputDir>\EndpointMonitoring_<version>.zip

    Both projects are published framework-dependent (the .NET 10 Hosting Bundle
    must be installed on the target, per doc\DEPLOYMENT.md).

    The produced .zip is self-contained: unzip it on the target server and run
    deploy.ps1 from an elevated PowerShell prompt.

.PARAMETER Configuration
    Build configuration. Default: Release (gives the CalVer version stamp).

.PARAMETER OutputDir
    Where publish output and the .zip are written. Default: <repo>\publish

.PARAMETER Runtime
    Optional RID (e.g. win-x64) for a self-contained publish. Leave empty for
    the default framework-dependent build.

.PARAMETER SkipWeb
    Do not publish the Web app.

.PARAMETER SkipService
    Do not publish the Monitoring Service.

.PARAMETER NoZip
    Publish only; do not create the .zip package.

.EXAMPLE
    .\publish.ps1

.EXAMPLE
    .\publish.ps1 -Configuration Release -OutputDir 'C:\artifacts'

.EXAMPLE
    .\publish.ps1 -Runtime win-x64   # self-contained, no Hosting Bundle needed
#>
[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [string] $OutputDir,
    [string] $Runtime,
    [switch] $SkipWeb,
    [switch] $SkipService,
    [switch] $NoZip
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Write-Step { param([string]$Message) Write-Host "==> $Message" -ForegroundColor Cyan }
function Write-Info { param([string]$Message) Write-Host "    $Message" -ForegroundColor Gray }
function Write-Ok   { param([string]$Message) Write-Host "    $Message" -ForegroundColor Green }

# ----------------------------------------------------------------- Paths -----
# <repo>\deploy\publish.ps1  ->  RepoRoot is the parent of the script folder.
$ScriptDir = $PSScriptRoot
$RepoRoot  = Split-Path $ScriptDir -Parent

$WebProject     = Join-Path $RepoRoot 'src\EndpointMonitoring.Web\EndpointMonitoring.Web.csproj'
$ServiceProject = Join-Path $RepoRoot 'src\EndpointMonitoring.MonitoringService\EndpointMonitoring.MonitoringService.csproj'
$DeployScript   = Join-Path $ScriptDir 'deploy.ps1'
$DeploymentDoc  = Join-Path $RepoRoot 'doc\DEPLOYMENT.md'

if (-not $OutputDir) { $OutputDir = Join-Path $RepoRoot 'publish' }
$WebOut     = Join-Path $OutputDir 'web'
$ServiceOut = Join-Path $OutputDir 'service'

foreach ($p in @($WebProject, $ServiceProject, $DeployScript)) {
    if (-not (Test-Path $p)) { throw "Required path not found: $p" }
}

# Fail early if dotnet isn't on PATH.
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "'dotnet' CLI not found on PATH. Install the .NET 10 SDK."
}

# ------------------------------------------------------------- Publish -------
# Wraps 'dotnet publish'. We pass -p:UseAppHost / RID only when a runtime is set.
function Invoke-Publish {
    param([string]$Project, [string]$Out, [string]$Label)

    Write-Step "Publishing $Label"
    if (Test-Path $Out) { Remove-Item -LiteralPath $Out -Recurse -Force }

    $publishArgs = @($Project, '-c', $Configuration, '-o', $Out, '--nologo')
    if ($Runtime) {
        $publishArgs += @('-r', $Runtime, '--self-contained', 'true')
        Write-Info "runtime: $Runtime (self-contained)"
    } else {
        Write-Info "framework-dependent (.NET 10 Hosting Bundle required on target)"
    }

    & dotnet publish @publishArgs
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $Label (exit $LASTEXITCODE)." }
    Write-Ok "$Label -> $Out"
}

if (-not $SkipWeb)     { Invoke-Publish -Project $WebProject     -Out $WebOut     -Label 'Web application' }
if (-not $SkipService) { Invoke-Publish -Project $ServiceProject -Out $ServiceOut -Label 'Monitoring service' }

# ------------------------------------------------------------- Version -------
# Read the stamped FileVersion from a published assembly to name the package.
function Get-PublishedVersion {
    $dll = @(
        (Join-Path $WebOut 'EndpointMonitoring.Web.dll'),
        (Join-Path $ServiceOut 'EndpointMonitoring.MonitoringService.dll')
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1

    if (-not $dll) { return 'unknown' }
    return [Diagnostics.FileVersionInfo]::GetVersionInfo($dll).FileVersion
}

# --------------------------------------------------------------- Package -----
if (-not $NoZip) {
    $version = Get-PublishedVersion
    $zipPath = Join-Path $OutputDir "EndpointMonitoring_$version.zip"

    Write-Step "Creating package"
    if (Test-Path $zipPath) { Remove-Item -LiteralPath $zipPath -Force }

    $items = @()
    if (-not $SkipWeb)     { $items += $WebOut }
    if (-not $SkipService) { $items += $ServiceOut }
    $items += $DeployScript
    if (Test-Path $DeploymentDoc) { $items += $DeploymentDoc }

    # Compress-Archive keeps each folder as a top-level entry (web\, service\)
    # and each file at the archive root (deploy.ps1, DEPLOYMENT.md).
    Compress-Archive -Path $items -DestinationPath $zipPath -CompressionLevel Optimal -Force

    $sizeMb = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
    Write-Ok "Package: $zipPath ($sizeMb MB, version $version)"
    Write-Host
    Write-Info "Copy this .zip to the target server, unzip it, then run from an"
    Write-Info "elevated PowerShell prompt:  .\deploy.ps1"
}

Write-Step "Done."
