#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Injects drivers into a WinPE WIM image or an offline Windows installation.

.DESCRIPTION
    Mounts a WIM file, uses DISM to inject drivers from a specified directory,
    then unmounts and commits the changes.

.PARAMETER WimPath
    Path to the WIM file to modify (e.g., boot.wim from a WinPE image).

.PARAMETER DriverPath
    Path to the directory containing driver .inf files.

.PARAMETER Recurse
    Search subdirectories for drivers. Defaults to true.

.PARAMETER Index
    WIM image index to mount. Defaults to 1.

.EXAMPLE
    .\Add-Drivers.ps1 -WimPath .\media\sources\boot.wim -DriverPath C:\Drivers\Dell

.EXAMPLE
    .\Add-Drivers.ps1 -WimPath D:\sources\install.wim -DriverPath C:\Drivers -Index 1
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$WimPath,

    [Parameter(Mandatory)]
    [string]$DriverPath,

    [bool]$Recurse = $true,

    [int]$Index = 1
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $WimPath)) {
    Write-Error "WIM file not found: $WimPath"
    exit 1
}

if (-not (Test-Path $DriverPath)) {
    Write-Error "Driver path not found: $DriverPath"
    exit 1
}

# Count .inf files
$infFiles = Get-ChildItem -Path $DriverPath -Filter "*.inf" -Recurse:$Recurse
Write-Host "Found $($infFiles.Count) driver .inf file(s) in '$DriverPath'" -ForegroundColor Cyan

if ($infFiles.Count -eq 0) {
    Write-Warning "No .inf files found. Nothing to inject."
    exit 0
}

# Create temp mount directory
$mountDir = Join-Path $env:TEMP "wim-mount-$(Get-Date -Format 'yyyyMMddHHmmss')"
New-Item -Path $mountDir -ItemType Directory -Force | Out-Null

try {
    # Mount WIM
    Write-Host "Mounting WIM (Index $Index)..." -ForegroundColor Yellow
    & dism /Mount-Wim /WimFile:$WimPath /Index:$Index /MountDir:$mountDir
    if ($LASTEXITCODE -ne 0) { Write-Error "Failed to mount WIM"; exit 1 }

    # Inject drivers
    Write-Host "Injecting drivers..." -ForegroundColor Yellow
    $dismArgs = "/Image:$mountDir /Add-Driver /Driver:$DriverPath"
    if ($Recurse) { $dismArgs += " /Recurse" }

    & dism $dismArgs.Split(" ")
    $dismExit = $LASTEXITCODE

    if ($dismExit -ne 0) {
        Write-Warning "DISM reported errors (exit code $dismExit). Some drivers may not have been injected."
    }
} finally {
    # Unmount and commit
    Write-Host "Unmounting WIM..." -ForegroundColor Yellow
    & dism /Unmount-Wim /MountDir:$mountDir /Commit
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Commit failed, attempting discard..."
        & dism /Unmount-Wim /MountDir:$mountDir /Discard
    }

    # Cleanup mount directory
    Remove-Item -Path $mountDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ""
Write-Host "Driver injection complete." -ForegroundColor Green
