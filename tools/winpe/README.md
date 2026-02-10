# WinPE Build Tools

Scripts for building a bootable WinPE ISO containing the ZeroInstall restore tool (`zim-winpe`).

## Prerequisites

1. **Windows Assessment and Deployment Kit (ADK)** — Install from [Microsoft ADK download page](https://learn.microsoft.com/en-us/windows-hardware/get-started/adk-install)
2. **WinPE add-on for the ADK** — Install from the same page (separate download)
3. **Administrator privileges** — Required for DISM operations and WIM mounting
4. **.NET 8 SDK** — To publish the `zim-winpe` project

## Quick Start

```powershell
# 1. Publish zim-winpe as a self-contained single-file executable
dotnet publish src/ZeroInstall.WinPE/ -c Release -r win-x64 --self-contained -p:PublishSingleFile=true

# 2. Build the WinPE ISO
.\tools\winpe\Build-WinPE.ps1 -OutputPath .\output\ZeroInstall-WinPE.iso

# 3. (Optional) Include drivers for target hardware
.\tools\winpe\Build-WinPE.ps1 -OutputPath .\output\ZeroInstall-WinPE.iso -IncludeDrivers C:\Drivers\Dell-Latitude
```

## Scripts

### Build-WinPE.ps1

Creates a complete bootable WinPE ISO with `zim-winpe` baked in.

| Parameter | Description | Default |
|-----------|-------------|---------|
| `-OutputPath` | Output ISO file path | `.\ZeroInstall-WinPE.iso` |
| `-Architecture` | Target arch (`amd64`, `x86`, `arm64`) | `amd64` |
| `-IncludeDrivers` | Driver directory to bake in | (none) |
| `-ZimWinPePath` | Path to published zim-winpe | Auto-detect |
| `-AdkPath` | ADK installation path | Default ADK path |

### Add-Drivers.ps1

Injects drivers into an existing WIM file (WinPE or offline Windows).

| Parameter | Description | Default |
|-----------|-------------|---------|
| `-WimPath` | WIM file to modify (required) | — |
| `-DriverPath` | Driver directory (required) | — |
| `-Recurse` | Search subdirectories | `$true` |
| `-Index` | WIM image index | `1` |

## Creating a Bootable USB

After building the ISO, write it to a USB drive using one of:

- **Rufus** (recommended): [rufus.ie](https://rufus.ie) — Select the ISO, choose GPT/UEFI or MBR/BIOS
- **ADK tools**: `MakeWinPEMedia.cmd /UFD <workdir> <drive-letter>`
- **dd** (Linux/WSL): `dd if=ZeroInstall-WinPE.iso of=/dev/sdX bs=4M`

## Boot Modes

The generated ISO supports both **UEFI** and **Legacy BIOS** boot, depending on the ADK version and `MakeWinPEMedia` configuration.

## See Also

- [PXE Boot Guide](../../docs/PXE-Boot-Guide.md) for network-based restore
- `zim-winpe --help` for command-line options
