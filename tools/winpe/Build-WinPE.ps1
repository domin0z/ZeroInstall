#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Builds a bootable WinPE ISO with the ZeroInstall restore tool (zim-winpe) baked in.

.DESCRIPTION
    Uses the Windows Assessment and Deployment Kit (ADK) to create a custom WinPE
    ISO image containing zim-winpe. Optionally includes drivers for the target hardware.

.PARAMETER OutputPath
    Path for the output ISO file. Defaults to .\ZeroInstall-WinPE.iso

.PARAMETER Architecture
    Target architecture. Defaults to amd64.

.PARAMETER IncludeDrivers
    Optional path to a directory containing drivers (.inf) to bake into the WinPE image.

.PARAMETER ZimWinPePath
    Path to the published zim-winpe directory (self-contained publish output).
    If not specified, attempts to find it at the default publish location.

.PARAMETER AdkPath
    Path to Windows ADK installation. Defaults to standard ADK install path.

.EXAMPLE
    .\Build-WinPE.ps1 -ZimWinPePath .\publish\zim-winpe -OutputPath .\output\ZeroInstall-WinPE.iso

.EXAMPLE
    .\Build-WinPE.ps1 -IncludeDrivers C:\Drivers\Dell-Latitude -Architecture amd64
#>

[CmdletBinding()]
param(
    [string]$OutputPath = ".\ZeroInstall-WinPE.iso",

    [ValidateSet("amd64", "x86", "arm64")]
    [string]$Architecture = "amd64",

    [string]$IncludeDrivers,

    [string]$ZimWinPePath,

    [string]$AdkPath = "${env:ProgramFiles(x86)}\Windows Kits\10\Assessment and Deployment Kit"
)

$ErrorActionPreference = "Stop"

# --- Validate prerequisites ---

$winPeAddon = Join-Path $AdkPath "Windows Preinstallation Environment"
$deploymentTools = Join-Path $AdkPath "Deployment Tools"
$copype = Join-Path $deploymentTools "copype.cmd"
$makeMedia = Join-Path $deploymentTools "MakeWinPEMedia.cmd"

if (-not (Test-Path $copype)) {
    Write-Error "Windows ADK not found at '$AdkPath'. Install the ADK and WinPE add-on from https://learn.microsoft.com/en-us/windows-hardware/get-started/adk-install"
    exit 1
}

if (-not (Test-Path $winPeAddon)) {
    Write-Error "WinPE add-on not found. Install the 'Windows PE add-on for the ADK' from https://learn.microsoft.com/en-us/windows-hardware/get-started/adk-install"
    exit 1
}

# Find zim-winpe
if (-not $ZimWinPePath) {
    $defaultPublish = Join-Path $PSScriptRoot "..\..\src\ZeroInstall.WinPE\bin\Release\net8.0-windows\win-x64\publish"
    if (Test-Path $defaultPublish) {
        $ZimWinPePath = $defaultPublish
    } else {
        Write-Error "Could not find zim-winpe publish output. Publish first with: dotnet publish src/ZeroInstall.WinPE/ -c Release -r win-x64 --self-contained -p:PublishSingleFile=true"
        exit 1
    }
}

if (-not (Test-Path (Join-Path $ZimWinPePath "zim-winpe.exe"))) {
    Write-Error "zim-winpe.exe not found in '$ZimWinPePath'. Ensure you published the WinPE project."
    exit 1
}

Write-Host "=== ZeroInstall WinPE ISO Builder ===" -ForegroundColor Cyan
Write-Host "Architecture: $Architecture"
Write-Host "Output:       $OutputPath"
Write-Host "zim-winpe:    $ZimWinPePath"
if ($IncludeDrivers) { Write-Host "Drivers:      $IncludeDrivers" }
Write-Host ""

# --- Step 1: Create WinPE working directory ---

$workDir = Join-Path $env:TEMP "ZeroInstall-WinPE-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
Write-Host "[1/6] Creating WinPE working copy..." -ForegroundColor Yellow

# Set up environment for ADK tools
$env:WinPERoot = $winPeAddon
$env:OSCDImgRoot = Join-Path $deploymentTools "OSCDIMG"

& $copype $Architecture $workDir
if ($LASTEXITCODE -ne 0) { Write-Error "copype.cmd failed"; exit 1 }

# --- Step 2: Mount boot.wim ---

$mountDir = Join-Path $workDir "mount"
$bootWim = Join-Path $workDir "media\sources\boot.wim"

Write-Host "[2/6] Mounting boot.wim..." -ForegroundColor Yellow
& dism /Mount-Wim /WimFile:$bootWim /Index:1 /MountDir:$mountDir
if ($LASTEXITCODE -ne 0) { Write-Error "Failed to mount boot.wim"; exit 1 }

try {
    # --- Step 3: Copy zim-winpe into the image ---

    $toolsDir = Join-Path $mountDir "ZeroInstall"
    Write-Host "[3/6] Copying zim-winpe into WinPE image..." -ForegroundColor Yellow
    New-Item -Path $toolsDir -ItemType Directory -Force | Out-Null
    Copy-Item -Path (Join-Path $ZimWinPePath "*") -Destination $toolsDir -Recurse -Force

    # --- Step 4: Create startnet.cmd to auto-launch zim-winpe ---

    $startnet = Join-Path $mountDir "Windows\System32\startnet.cmd"
    @"
@echo off
wpeinit
echo.
echo  Starting ZeroInstall WinPE Restore...
echo.
X:\ZeroInstall\zim-winpe.exe
cmd.exe
"@ | Set-Content -Path $startnet -Encoding ASCII

    # --- Step 5: Inject drivers (optional) ---

    if ($IncludeDrivers -and (Test-Path $IncludeDrivers)) {
        Write-Host "[4/6] Injecting drivers from '$IncludeDrivers'..." -ForegroundColor Yellow
        & dism /Image:$mountDir /Add-Driver /Driver:$IncludeDrivers /Recurse
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Some drivers may have failed to inject. Continuing..."
        }
    } else {
        Write-Host "[4/6] No drivers to inject, skipping..." -ForegroundColor DarkGray
    }

    # --- Step 6: Unmount and commit ---

    Write-Host "[5/6] Unmounting and committing changes..." -ForegroundColor Yellow
} finally {
    & dism /Unmount-Wim /MountDir:$mountDir /Commit
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Unmount with commit failed, attempting discard..."
        & dism /Unmount-Wim /MountDir:$mountDir /Discard
    }
}

# --- Step 7: Create ISO ---

Write-Host "[6/6] Creating bootable ISO..." -ForegroundColor Yellow

$outputDir = Split-Path $OutputPath -Parent
if ($outputDir -and -not (Test-Path $outputDir)) {
    New-Item -Path $outputDir -ItemType Directory -Force | Out-Null
}

& $makeMedia /ISO $workDir $OutputPath
if ($LASTEXITCODE -ne 0) { Write-Error "MakeWinPEMedia.cmd failed"; exit 1 }

# --- Cleanup ---

Write-Host ""
Write-Host "Cleaning up working directory..."
Remove-Item -Path $workDir -Recurse -Force -ErrorAction SilentlyContinue

$isoSize = (Get-Item $OutputPath).Length / 1MB
Write-Host ""
Write-Host "=== BUILD COMPLETE ===" -ForegroundColor Green
Write-Host "ISO:  $OutputPath ($([math]::Round($isoSize, 1)) MB)"
Write-Host ""
Write-Host "To create a bootable USB from this ISO:" -ForegroundColor Cyan
Write-Host "  1. Insert USB drive"
Write-Host "  2. Run: MakeWinPEMedia.cmd /UFD $workDir X:"
Write-Host "     (where X: is the USB drive letter)"
Write-Host ""
Write-Host "Or use Rufus (https://rufus.ie) to write the ISO to USB."
