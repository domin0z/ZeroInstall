<#
.SYNOPSIS
    Updates the version numbers in Directory.Build.props.

.DESCRIPTION
    Sets Version, AssemblyVersion, and FileVersion properties in the shared
    Directory.Build.props file. Accepts semantic version (e.g., 1.2.3) and
    computes AssemblyVersion/FileVersion as Major.Minor.Patch.0.

.PARAMETER Version
    The semantic version to set (e.g., 1.0.0, 0.2.0-beta1).

.PARAMETER PropsFile
    Path to Directory.Build.props. Defaults to repo root.

.EXAMPLE
    .\Set-Version.ps1 -Version 1.0.0

.EXAMPLE
    .\Set-Version.ps1 -Version 2.1.0-rc1
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Version,

    [string]$PropsFile
)

$ErrorActionPreference = "Stop"

# Resolve props file path
if (-not $PropsFile) {
    $PropsFile = Join-Path (Split-Path $PSScriptRoot -Parent) "Directory.Build.props"
}

$PropsFile = Resolve-Path $PropsFile

# Validate version format (semver with optional prerelease)
if ($Version -notmatch '^\d+\.\d+\.\d+(-[\w.]+)?$') {
    Write-Error "Invalid version format: '$Version'. Expected format: Major.Minor.Patch (e.g., 1.0.0) or Major.Minor.Patch-prerelease (e.g., 1.0.0-rc1)."
    exit 1
}

# Extract Major.Minor.Patch for assembly version (strip prerelease suffix)
$coreParts = $Version -replace '-.*$', ''
$assemblyVersion = "$coreParts.0"

Write-Host "Updating version in: $PropsFile" -ForegroundColor Cyan
Write-Host "  Version:         $Version" -ForegroundColor White
Write-Host "  AssemblyVersion: $assemblyVersion" -ForegroundColor White
Write-Host "  FileVersion:     $assemblyVersion" -ForegroundColor White

# Read the file content
$content = Get-Content $PropsFile -Raw

# Replace version elements
$content = $content -replace '<Version>[^<]+</Version>', "<Version>$Version</Version>"
$content = $content -replace '<AssemblyVersion>[^<]+</AssemblyVersion>', "<AssemblyVersion>$assemblyVersion</AssemblyVersion>"
$content = $content -replace '<FileVersion>[^<]+</FileVersion>', "<FileVersion>$assemblyVersion</FileVersion>"

# Write back
Set-Content -Path $PropsFile -Value $content -NoNewline

Write-Host ""
Write-Host "Version updated successfully." -ForegroundColor Green
