#Requires -Version 5.1
<#
.SYNOPSIS
    Deploys (updates) the Endpoint Monitoring Web app and Monitoring Service on a
    Windows Server, in place, preserving local configuration and database files.

.DESCRIPTION
    Intended to be run ON THE TARGET SERVER in an elevated PowerShell session.
    It is shipped inside the deployment package produced by publish.ps1 and, by
    default, picks up the 'web' and 'service' folders that sit next to it.

    For each component the script:
      1. Stops the IIS site + application pool (web) / the Windows service (service).
      2. Overwrites the target folder with the new build, deleting old binaries
         first but PRESERVING files that match -PreservePatterns
         (appsettings*.json and the SQLite database by default).
      3. Starts the application pool + site / the Windows service again.

    Every setting can be supplied as a parameter OR changed once in the
    "Defaults" block below so the script can simply be double-run on the target.

    NOTE: This script UPDATES an existing installation. First-time setup
    (creating the IIS site / app pool and the Windows service) is described in
    doc\DEPLOYMENT.md and is intentionally NOT automated here.

.PARAMETER WebSource
    Folder containing the new Web build. Default: <script dir>\web

.PARAMETER ServiceSource
    Folder containing the new Service build. Default: <script dir>\service

.PARAMETER WebTarget
    IIS physical path the Web app is deployed to.

.PARAMETER ServiceTarget
    Folder the Windows Service binaries live in.

.PARAMETER SiteName
    IIS site name to stop/start.

.PARAMETER AppPoolName
    IIS application pool name to stop/start.

.PARAMETER ServiceName
    Windows service name to stop/start.

.PARAMETER PreservePatterns
    Wildcard patterns (relative to the target folder, recursive) that must NOT
    be overwritten or deleted during the update.

.PARAMETER SkipWeb
    Do not touch the Web app.

.PARAMETER SkipService
    Do not touch the Monitoring Service.

.PARAMETER StopTimeoutSeconds
    How long to wait for the service / app pool to stop before giving up.

.EXAMPLE
    # Use all defaults (run from inside the unzipped package, elevated):
    .\deploy.ps1

.EXAMPLE
    # Override targets and names for a different environment:
    .\deploy.ps1 -WebTarget 'D:\Sites\monitoring' -SiteName 'monitoring.contoso.de' `
                 -AppPoolName 'monitoring.contoso.de' -ServiceName 'EndpointMonitoringService'

.EXAMPLE
    # Deploy only the service:
    .\deploy.ps1 -SkipWeb
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    # ---------------------------------------------------------------- Defaults
    # Change these defaults to bake environment settings into the script, or
    # override any of them on the command line.

    [string]   $WebSource         = (Join-Path $PSScriptRoot 'web'),
    [string]   $ServiceSource     = (Join-Path $PSScriptRoot 'service'),

    [string]   $WebTarget         = 'C:\inetpub\wwwroot\endpoint-monitoring',
    [string]   $ServiceTarget     = 'C:\Services\endpoint-monitoring',

    [string]   $SiteName          = 'EndpointMonitoring',
    [string]   $AppPoolName       = 'EndpointMonitoring',
    [string]   $ServiceName       = 'EndpointMonitoringService',

    [string[]] $PreservePatterns  = @('appsettings*.json', '*.db', '*.db-wal', '*.db-shm'),

    [switch]   $SkipWeb,
    [switch]   $SkipService,
    [int]      $StopTimeoutSeconds = 60
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# ----------------------------------------------------------------- Helpers ---

function Write-Step { param([string]$Message) Write-Host "==> $Message" -ForegroundColor Cyan }
function Write-Info { param([string]$Message) Write-Host "    $Message" -ForegroundColor Gray }
function Write-Ok   { param([string]$Message) Write-Host "    $Message" -ForegroundColor Green }
function Write-Warn { param([string]$Message) Write-Warning $Message }

function Assert-Administrator {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($id)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "This script must be run from an elevated (Run as administrator) PowerShell session."
    }
}

# Copies preserved files out of $Target into a temp folder so they survive the
# wipe, then returns the temp folder path (or $null when nothing was preserved).
function Backup-PreservedFiles {
    param([string]$Target, [string[]]$Patterns)

    if (-not (Test-Path $Target)) { return $null }

    $found = foreach ($pattern in $Patterns) {
        Get-ChildItem -Path $Target -Filter $pattern -Recurse -File -ErrorAction SilentlyContinue
    }
    $found = $found | Sort-Object FullName -Unique
    if (-not $found) { return $null }

    $stash = Join-Path ([IO.Path]::GetTempPath()) ("emdeploy_" + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $stash -Force | Out-Null

    foreach ($file in $found) {
        $relative = $file.FullName.Substring($Target.TrimEnd('\').Length).TrimStart('\')
        $dest     = Join-Path $stash $relative
        New-Item -ItemType Directory -Path (Split-Path $dest -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $dest -Force
        Write-Info "preserved: $relative"
    }
    return $stash
}

# Restores stashed files back into $Target (overwriting whatever the new build
# brought), then removes the temp folder.
function Restore-PreservedFiles {
    param([string]$Stash, [string]$Target)

    if (-not $Stash -or -not (Test-Path $Stash)) { return }

    Get-ChildItem -Path $Stash -Recurse -File | ForEach-Object {
        $relative = $_.FullName.Substring($Stash.TrimEnd('\').Length).TrimStart('\')
        $dest     = Join-Path $Target $relative
        New-Item -ItemType Directory -Path (Split-Path $dest -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $_.FullName -Destination $dest -Force
        Write-Info "restored:  $relative"
    }
    Remove-Item -LiteralPath $Stash -Recurse -Force -ErrorAction SilentlyContinue
}

# Deletes the contents of a folder, retrying on locked-file errors. A just-
# stopped service or IIS worker process can hold handles open for a short while
# after it reports stopped, which otherwise surfaces as "Access to the path ...
# is denied" mid-delete.
function Remove-FolderContents {
    param([string]$Target, [int]$Retries = 10, [int]$DelayMs = 1000)

    for ($attempt = 1; ; $attempt++) {
        try {
            Get-ChildItem -Path $Target -Force | Remove-Item -Recurse -Force -ErrorAction Stop
            return
        } catch {
            if ($attempt -ge $Retries) {
                throw "Could not clear '$Target' after $Retries attempts - a file is still locked. " +
                      "Make sure the service / app pool is fully stopped. Original error: $($_.Exception.Message)"
            }
            Write-Info "files still locked (attempt $attempt/$Retries) - retrying in $([math]::Round($DelayMs/1000,1))s..."
            Start-Sleep -Milliseconds $DelayMs
        }
    }
}

# Deletes everything in $Target except the preserved files (which were stashed),
# then copies the new build in, then restores the preserved files on top.
function Update-Folder {
    param([string]$Source, [string]$Target, [string[]]$Patterns)

    if (-not (Test-Path $Source)) {
        throw "Source folder not found: $Source"
    }
    if (-not (Get-ChildItem -Path $Source -ErrorAction SilentlyContinue)) {
        throw "Source folder is empty: $Source"
    }

    if ($WhatIfPreference) {
        Write-Info "What if: would clear $Target and copy new build from $Source"
        Write-Info "What if: would preserve $($Patterns -join ', ')"
        return
    }

    $stash = Backup-PreservedFiles -Target $Target -Patterns $Patterns

    if (Test-Path $Target) {
        Write-Info "clearing old files in $Target"
        Remove-FolderContents -Target $Target
    } else {
        New-Item -ItemType Directory -Path $Target -Force | Out-Null
    }

    Write-Info "copying new build -> $Target"
    Copy-Item -Path (Join-Path $Source '*') -Destination $Target -Recurse -Force

    Restore-PreservedFiles -Stash $stash -Target $Target
}

# ------------------------------------------------------------- IIS helpers ---

function Use-WebAdministration {
    if (-not (Get-Module -Name WebAdministration)) {
        Import-Module WebAdministration -ErrorAction Stop
    }
}

function Stop-IisComponents {
    Use-WebAdministration

    if (Test-Path "IIS:\AppPools\$AppPoolName") {
        $state = (Get-WebAppPoolState -Name $AppPoolName).Value
        if ($state -ne 'Stopped') {
            if ($WhatIfPreference) {
                Write-Info "What if: would stop app pool '$AppPoolName'"
            } else {
                Write-Info "stopping app pool '$AppPoolName'"
                Stop-WebAppPool -Name $AppPoolName
                Wait-ForState -Get { (Get-WebAppPoolState -Name $AppPoolName).Value } -Desired 'Stopped' -Label "app pool '$AppPoolName'"
            }
        } else {
            Write-Info "app pool '$AppPoolName' already stopped"
        }
    } else {
        Write-Warn "App pool '$AppPoolName' not found - skipping (is the site set up?)."
    }

    if (Test-Path "IIS:\Sites\$SiteName") {
        $state = (Get-WebsiteState -Name $SiteName).Value
        if ($state -ne 'Stopped') {
            if ($WhatIfPreference) {
                Write-Info "What if: would stop site '$SiteName'"
            } else {
                Write-Info "stopping site '$SiteName'"
                Stop-Website -Name $SiteName
            }
        } else {
            Write-Info "site '$SiteName' already stopped"
        }
    } else {
        Write-Warn "Site '$SiteName' not found - skipping."
    }
}

function Start-IisComponents {
    Use-WebAdministration

    if (Test-Path "IIS:\AppPools\$AppPoolName") {
        if ($WhatIfPreference) {
            Write-Info "What if: would start app pool '$AppPoolName'"
        } else {
            Write-Info "starting app pool '$AppPoolName'"
            Start-WebAppPool -Name $AppPoolName
        }
    }
    if (Test-Path "IIS:\Sites\$SiteName") {
        if ($WhatIfPreference) {
            Write-Info "What if: would start site '$SiteName'"
        } else {
            Write-Info "starting site '$SiteName'"
            Start-Website -Name $SiteName
        }
    }
}

# --------------------------------------------------------- service helpers ---

function Stop-MonitoringService {
    $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if (-not $svc) {
        Write-Warn "Service '$ServiceName' not found - skipping stop (run first-time setup from DEPLOYMENT.md)."
        return $false
    }
    if ($svc.Status -ne 'Stopped') {
        if ($WhatIfPreference) {
            Write-Info "What if: would stop service '$ServiceName'"
        } else {
            # Capture the worker PID *before* stopping. The SCM reports 'Stopped'
            # as soon as the stop is acknowledged, but the process can take a
            # moment longer to exit and release its file handles - so we wait for
            # the process itself to be gone, not just the service status.
            $procId = [int](Get-CimInstance Win32_Service -Filter "Name='$ServiceName'" -ErrorAction SilentlyContinue).ProcessId

            Write-Info "stopping service '$ServiceName'"
            Stop-Service -Name $ServiceName -Force
            Wait-ForState -Get { (Get-Service -Name $ServiceName).Status.ToString() } -Desired 'Stopped' -Label "service '$ServiceName'"

            if ($procId -gt 0) {
                Wait-ForProcessExit -Id $procId -Label "service '$ServiceName'"
            }
        }
    } else {
        Write-Info "service '$ServiceName' already stopped"
    }
    return $true
}

function Start-MonitoringService {
    $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($svc) {
        if ($WhatIfPreference) {
            Write-Info "What if: would start service '$ServiceName'"
        } else {
            Write-Info "starting service '$ServiceName'"
            Start-Service -Name $ServiceName
        }
    }
}

# Polls a state getter until it reaches $Desired or the timeout elapses.
function Wait-ForState {
    param([scriptblock]$Get, [string]$Desired, [string]$Label)

    $deadline = (Get-Date).AddSeconds($StopTimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if ((& $Get) -eq $Desired) { return }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out after ${StopTimeoutSeconds}s waiting for $Label to reach state '$Desired'."
}

# Waits until the given process id has exited (so its file handles are released).
function Wait-ForProcessExit {
    param([int]$Id, [string]$Label)

    $deadline = (Get-Date).AddSeconds($StopTimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (-not (Get-Process -Id $Id -ErrorAction SilentlyContinue)) {
            Write-Info "$Label process (PID $Id) exited"
            return
        }
        Start-Sleep -Milliseconds 500
    }
    Write-Warn "$Label process (PID $Id) still running after ${StopTimeoutSeconds}s; continuing anyway."
}

# --------------------------------------------------------------------- Main ---

Assert-Administrator

Write-Step "Endpoint Monitoring deployment"
Write-Info  "Web:     $(if ($SkipWeb)     {'(skipped)'} else {"$WebSource -> $WebTarget"})"
Write-Info  "Service: $(if ($SkipService) {'(skipped)'} else {"$ServiceSource -> $ServiceTarget"})"
Write-Info  "Preserve: $($PreservePatterns -join ', ')"
Write-Host

# ---- Web ----
if (-not $SkipWeb) {
    Write-Step "Web application ('$SiteName')"
    Stop-IisComponents
    Update-Folder -Source $WebSource -Target $WebTarget -Patterns $PreservePatterns
    Start-IisComponents
    Write-Ok "Web application updated."
    Write-Host
}

# ---- Service ----
if (-not $SkipService) {
    Write-Step "Monitoring service ('$ServiceName')"
    $existed = Stop-MonitoringService
    Update-Folder -Source $ServiceSource -Target $ServiceTarget -Patterns $PreservePatterns
    if ($existed) { Start-MonitoringService }
    Write-Ok "Monitoring service updated."
    Write-Host
}

Write-Step "Done."
