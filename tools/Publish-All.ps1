<#
.SYNOPSIS
    Publishes all ZeroInstall Migrator executables and assembles the portable distribution.

.DESCRIPTION
    Builds all 4 executable projects as self-contained, single-file, win-x64 binaries.
    Assembles the portable USB folder structure with profiles, docs, and tools.
    Optionally signs binaries if a certificate thumbprint is provided.
    Creates a ZIP archive of the distribution.

.PARAMETER OutputDir
    Root directory for build output. Defaults to .\dist (relative to repo root).

.PARAMETER Version
    Version to stamp into the binaries. If not specified, reads from Directory.Build.props.

.PARAMETER Configuration
    Build configuration. Defaults to Release.

.PARAMETER Runtime
    Target runtime identifier. Defaults to win-x64.

.PARAMETER CertThumbprint
    Optional code signing certificate thumbprint. If provided, signs all binaries after publish.

.PARAMETER SkipTests
    Skip running tests before publish.

.PARAMETER SkipZip
    Skip creating the ZIP archive.

.EXAMPLE
    .\Publish-All.ps1

.EXAMPLE
    .\Publish-All.ps1 -Version 1.0.0 -CertThumbprint "ABC123..."

.EXAMPLE
    .\Publish-All.ps1 -OutputDir D:\release -SkipTests
#>

[CmdletBinding()]
param(
    [string]$OutputDir,
    [string]$Version,
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$CertThumbprint,
    [switch]$SkipTests,
    [switch]$SkipZip
)

$ErrorActionPreference = "Stop"

# --- Resolve paths ---
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")

if (-not $OutputDir) {
    $OutputDir = Join-Path $repoRoot "dist"
}

$distDir = Join-Path $OutputDir "ZeroInstall"
$tempPublishRoot = Join-Path $OutputDir "_publish_temp"

# --- Resolve version ---
if (-not $Version) {
    [xml]$props = Get-Content (Join-Path $repoRoot "Directory.Build.props")
    $Version = $props.Project.PropertyGroup.Version
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  ZeroInstall Migrator - Publish" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Version:       $Version" -ForegroundColor White
Write-Host "  Configuration: $Configuration" -ForegroundColor White
Write-Host "  Runtime:       $Runtime" -ForegroundColor White
Write-Host "  Output:        $OutputDir" -ForegroundColor White
Write-Host ""

# --- Step 1: Clean output ---
Write-Host "[1/7] Cleaning output directory..." -ForegroundColor Cyan

if (Test-Path $distDir) {
    Remove-Item $distDir -Recurse -Force
}
if (Test-Path $tempPublishRoot) {
    Remove-Item $tempPublishRoot -Recurse -Force
}

New-Item -Path $distDir -ItemType Directory -Force | Out-Null

# --- Step 2: Run tests ---
if (-not $SkipTests) {
    Write-Host ""
    Write-Host "[2/7] Running tests..." -ForegroundColor Cyan

    $slnPath = Join-Path $repoRoot "ZeroInstall.sln"
    & dotnet test $slnPath -c $Configuration --verbosity minimal

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Tests failed. Fix test failures before publishing."
        exit 1
    }

    Write-Host "  All tests passed." -ForegroundColor Green
}
else {
    Write-Host ""
    Write-Host '[2/7] Skipping tests (-SkipTests).' -ForegroundColor Yellow
}

# --- Step 3: Publish each project ---
Write-Host ""
Write-Host "[3/7] Publishing projects..." -ForegroundColor Cyan

$projects = @(
    @{
        Name      = "ZeroInstall.App"
        Path      = "src\ZeroInstall.App\ZeroInstall.App.csproj"
        DestDir   = $distDir
        CopyAll   = $true   # WPF needs co-located runtime DLLs
    },
    @{
        Name      = "ZeroInstall.CLI"
        Path      = "src\ZeroInstall.CLI\ZeroInstall.CLI.csproj"
        DestDir   = $distDir
        CopyAll   = $false
    },
    @{
        Name      = "ZeroInstall.Agent"
        Path      = "src\ZeroInstall.Agent\ZeroInstall.Agent.csproj"
        DestDir   = $distDir
        CopyAll   = $false
    },
    @{
        Name      = "ZeroInstall.WinPE"
        Path      = "src\ZeroInstall.WinPE\ZeroInstall.WinPE.csproj"
        DestDir   = (Join-Path $distDir "winpe")
        CopyAll   = $false
    }
)

foreach ($proj in $projects) {
    $projPath = Join-Path $repoRoot $proj.Path
    $tempPublish = Join-Path $tempPublishRoot $proj.Name

    Write-Host "  Publishing $($proj.Name)..." -ForegroundColor White

    & dotnet publish $projPath `
        -c $Configuration `
        -r $Runtime `
        --self-contained `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:Version=$Version `
        -o $tempPublish

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to publish $($proj.Name)."
        exit 1
    }

    # Ensure destination directory exists
    New-Item -Path $proj.DestDir -ItemType Directory -Force | Out-Null

    if ($proj.CopyAll) {
        # WPF app: copy all files except PDBs (exe + runtime DLLs)
        Get-ChildItem -Path $tempPublish -Exclude "*.pdb" | Copy-Item -Destination $proj.DestDir -Recurse -Force
    }
    else {
        # Single-file: copy only the exe
        $exeFiles = Get-ChildItem -Path $tempPublish -Filter "*.exe"
        foreach ($exe in $exeFiles) {
            Copy-Item $exe.FullName $proj.DestDir -Force
        }
    }

    Write-Host "    -> $($proj.DestDir)" -ForegroundColor Green
}

# --- Step 4: Assemble distribution structure ---
Write-Host ""
Write-Host "[4/7] Assembling distribution structure..." -ForegroundColor Cyan

# Copy WinPE build scripts
Write-Host "  Copying WinPE scripts..." -ForegroundColor White
Copy-Item (Join-Path $repoRoot "tools\winpe\Build-WinPE.ps1") (Join-Path $distDir "winpe") -Force
Copy-Item (Join-Path $repoRoot "tools\winpe\Add-Drivers.ps1") (Join-Path $distDir "winpe") -Force

# Copy profiles
Write-Host "  Copying profiles..." -ForegroundColor White
$profilesDir = Join-Path $distDir "profiles"
New-Item -Path $profilesDir -ItemType Directory -Force | Out-Null
Copy-Item (Join-Path $repoRoot "profiles\*") $profilesDir -Recurse -Force

# Copy docs
Write-Host "  Copying documentation..." -ForegroundColor White
$docsDir = Join-Path $distDir "docs"
New-Item -Path $docsDir -ItemType Directory -Force | Out-Null
Copy-Item (Join-Path $repoRoot "docs\CLI-Reference.txt") $docsDir -Force
Copy-Item (Join-Path $repoRoot "docs\PXE-Boot-Guide.md") $docsDir -Force

# Copy distribution README
Write-Host "  Copying README..." -ForegroundColor White
Copy-Item (Join-Path $repoRoot "dist\README.md") $distDir -Force

# Create runtime directories
Write-Host "  Creating runtime directories..." -ForegroundColor White
New-Item -Path (Join-Path $distDir "config") -ItemType Directory -Force | Out-Null
New-Item -Path (Join-Path $distDir "jobs") -ItemType Directory -Force | Out-Null

# --- Step 5: Code signing (optional) ---
if ($CertThumbprint) {
    Write-Host ""
    Write-Host "[5/7] Signing binaries..." -ForegroundColor Cyan

    $signScript = Join-Path $PSScriptRoot "Sign-Binaries.ps1"
    & $signScript -DistPath $distDir -CertThumbprint $CertThumbprint

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Code signing failed."
        exit 1
    }
}
else {
    Write-Host ""
    Write-Host "[5/7] Skipping code signing (no -CertThumbprint provided)." -ForegroundColor Yellow
}

# --- Step 6: Create ZIP archive ---
if (-not $SkipZip) {
    Write-Host ""
    Write-Host "[6/7] Creating ZIP archive..." -ForegroundColor Cyan

    $zipName = "ZeroInstall-v$Version-$Runtime.zip"
    $zipPath = Join-Path $OutputDir $zipName

    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }

    Compress-Archive -Path $distDir -DestinationPath $zipPath -Force
    $zipSize = (Get-Item $zipPath).Length / 1MB
    $sizeMBStr = [math]::Round($zipSize, 1)
    Write-Host "  Created: $zipName - $sizeMBStr MB" -ForegroundColor Green
}
else {
    Write-Host ""
    Write-Host '[6/7] Skipping ZIP creation (-SkipZip).' -ForegroundColor Yellow
}

# --- Step 7: Clean up temp ---
Write-Host ""
Write-Host "[7/7] Cleaning up..." -ForegroundColor Cyan

if (Test-Path $tempPublishRoot) {
    try {
        Remove-Item $tempPublishRoot -Recurse -Force -ErrorAction Stop
    }
    catch {
        Write-Host "  Warning: Could not fully clean temp directory (may be locked by another process)." -ForegroundColor Yellow
    }
}

# --- Summary ---
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Publish Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Distribution: $distDir" -ForegroundColor White
Write-Host "  Version:      $Version" -ForegroundColor White
Write-Host ""

# List executables with sizes
$exes = Get-ChildItem -Path $distDir -Recurse -Filter "*.exe"
foreach ($exe in $exes) {
    $relativePath = $exe.FullName.Substring($distDir.Length + 1)
    $sizeMB = [math]::Round($exe.Length / 1MB, 1)
    Write-Host "  $relativePath - $sizeMB MB" -ForegroundColor White
}

Write-Host ""
Write-Host "  Copy the distribution folder to a USB drive to deploy." -ForegroundColor Cyan
