<#
.SYNOPSIS
    Signs all executables and DLLs in a distribution folder using Authenticode.

.DESCRIPTION
    Finds signtool.exe from the Windows SDK, then signs all .exe and .dll files
    in the specified folder using the provided certificate thumbprint and timestamp
    server. Verifies signatures after signing.

.PARAMETER DistPath
    Path to the distribution folder containing binaries to sign.

.PARAMETER CertThumbprint
    SHA1 thumbprint of the code signing certificate in the Windows certificate store.

.PARAMETER TimestampServer
    RFC 3161 timestamp server URL. Defaults to DigiCert.

.PARAMETER SignToolPath
    Optional explicit path to signtool.exe. Auto-detected from Windows SDK if not specified.

.EXAMPLE
    .\Sign-Binaries.ps1 -DistPath .\dist\ZeroInstall -CertThumbprint "ABC123DEF456..."

.EXAMPLE
    .\Sign-Binaries.ps1 -DistPath .\dist\ZeroInstall -CertThumbprint "ABC123..." -TimestampServer "http://timestamp.sectigo.com"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$DistPath,

    [Parameter(Mandatory)]
    [string]$CertThumbprint,

    [string]$TimestampServer = "http://timestamp.digicert.com",

    [string]$SignToolPath
)

$ErrorActionPreference = "Stop"

# --- Step 1: Locate signtool.exe ---
Write-Host ""
Write-Host "[1/4] Locating signtool.exe..." -ForegroundColor Cyan

if ($SignToolPath -and (Test-Path $SignToolPath)) {
    Write-Host "  Using specified path: $SignToolPath" -ForegroundColor White
}
else {
    # Search Windows SDK locations
    $sdkPaths = Get-ChildItem -Path "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue |
        Sort-Object { [version]($_.Directory.Parent.Name -replace '\.0$', '') } -Descending

    if ($sdkPaths.Count -eq 0) {
        Write-Error @"
signtool.exe not found. Please install the Windows SDK:
  1. Download from https://developer.microsoft.com/en-us/windows/downloads/windows-sdk/
  2. During installation, select "Windows SDK Signing Tools for Desktop Apps"
  3. Re-run this script

Or specify the path directly: -SignToolPath "C:\path\to\signtool.exe"
"@
        exit 1
    }

    $SignToolPath = $sdkPaths[0].FullName
    Write-Host "  Found: $SignToolPath" -ForegroundColor White
}

# --- Step 2: Enumerate binaries ---
Write-Host ""
Write-Host "[2/4] Enumerating binaries in: $DistPath" -ForegroundColor Cyan

$DistPath = Resolve-Path $DistPath
$binaries = Get-ChildItem -Path $DistPath -Recurse -Include *.exe, *.dll

if ($binaries.Count -eq 0) {
    Write-Host "  No .exe or .dll files found. Nothing to sign." -ForegroundColor Yellow
    exit 0
}

Write-Host "  Found $($binaries.Count) file(s) to sign." -ForegroundColor White

# --- Step 3: Sign each file ---
Write-Host ""
Write-Host "[3/4] Signing binaries..." -ForegroundColor Cyan

$signedCount = 0
$failCount = 0

foreach ($file in $binaries) {
    $relativePath = $file.FullName.Substring($DistPath.Path.Length + 1)
    Write-Host "  Signing: $relativePath" -ForegroundColor White -NoNewline

    $result = & $SignToolPath sign /sha1 $CertThumbprint /fd sha256 /tr $TimestampServer /td sha256 /q $file.FullName 2>&1

    if ($LASTEXITCODE -eq 0) {
        Write-Host " OK" -ForegroundColor Green
        $signedCount++
    }
    else {
        Write-Host " FAILED" -ForegroundColor Red
        Write-Host "    $result" -ForegroundColor Red
        $failCount++
    }
}

# --- Step 4: Verify signatures ---
Write-Host ""
Write-Host "[4/4] Verifying signatures..." -ForegroundColor Cyan

$verifiedCount = 0
$verifyFailCount = 0

foreach ($file in $binaries) {
    $relativePath = $file.FullName.Substring($DistPath.Path.Length + 1)
    $result = & $SignToolPath verify /pa /q $file.FullName 2>&1

    if ($LASTEXITCODE -eq 0) {
        $verifiedCount++
    }
    else {
        Write-Host "  Verify failed: $relativePath" -ForegroundColor Red
        $verifyFailCount++
    }
}

# --- Summary ---
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Code Signing Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Signed:    $signedCount / $($binaries.Count)" -ForegroundColor $(if ($failCount -eq 0) { "Green" } else { "Yellow" })
Write-Host "  Verified:  $verifiedCount / $($binaries.Count)" -ForegroundColor $(if ($verifyFailCount -eq 0) { "Green" } else { "Yellow" })

if ($failCount -gt 0 -or $verifyFailCount -gt 0) {
    Write-Host "  Failures:  $($failCount + $verifyFailCount)" -ForegroundColor Red
    Write-Host ""
    Write-Error "Code signing completed with errors."
    exit 1
}

Write-Host ""
Write-Host "All binaries signed and verified successfully." -ForegroundColor Green
