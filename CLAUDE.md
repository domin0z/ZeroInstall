# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**ZeroInstall Migrator** is a portable Windows application for PC technicians to migrate data, settings, and applications from a customer's old computer to a new one. It is modeled after [Zinstall WinWin](https://www.zinstall.com) but is open-source under the GNU GPL license.

The application is designed to be run from a technician's USB flash drive with no installation required on either machine (portable-first). An optional lightweight agent can be installed on source/destination machines for WiFi-based transfers.

## Tech Stack

- **Language:** C# / .NET 8+
- **GUI Framework:** WPF (XAML)
- **Target Platform:** Windows 10/11 (x64), with WinPE for image restore
- **Build System:** MSBuild / `dotnet` CLI
- **License:** GNU GPL

## Architecture

### Solution Structure

```
ZeroInstall/
├── src/
│   ├── ZeroInstall.Core/              # Shared library: migration engine, models, utilities
│   ├── ZeroInstall.App/               # WPF portable application (technician GUI)
│   ├── ZeroInstall.Agent/             # Lightweight transfer agent (runs on source/dest)
│   ├── ZeroInstall.CLI/               # Command-line interface for scripting/automation
│   └── ZeroInstall.WinPE/            # WinPE restore tool and ISO/PXE builder
├── tests/
│   ├── ZeroInstall.Core.Tests/
│   ├── ZeroInstall.App.Tests/
│   └── ZeroInstall.Agent.Tests/
├── tools/                             # Build scripts, WinPE builder, signing tools
├── docs/                              # Architecture docs, technician guides
└── profiles/                          # Default migration profile templates
```

### Core Components

#### 1. Migration Engine (`ZeroInstall.Core`)
The central library consumed by all frontends (App, CLI, Agent). Contains:

- **Discovery Module** — Scans source machine to inventory installed applications (Add/Remove Programs, registry, winget/chocolatey detection), user profiles, system settings, files.
- **Transfer Pipeline** — Orchestrates the migration with a pluggable architecture:
  - **Package-Based Migrator** (Tier 1) — Identifies apps available via winget/chocolatey, generates install manifests for clean reinstall on destination, then overlays user settings/data.
  - **Registry+File Capture Migrator** (Tier 2 fallback) — For apps not in package repos: captures registry keys (HKLM/HKCU), Program Files, AppData, and replays on destination.
  - **Full Disk Cloner** (Tier 3 nuclear option) — Captures entire volume to .img, .raw, or .vhdx. Used when per-app migration fails and customer absolutely needs their apps.
- **Profile/Settings Migrator** — Transfers user profiles, documents, browser data, WiFi passwords, printers, mapped drives, environment variables, scheduled tasks, credentials, certificates.
- **Multi-User Support** — Source machines may have multiple user profiles. The technician selects which users to migrate and maps each source user to a destination user. If the destination user doesn't exist, the tool offers to create the local account. Each user mapping is independent (e.g., source "Bill" → dest "William", source "Admin" → dest "Admin").
- **User Path Remapper** — When the source and destination usernames differ (e.g., `C:\Users\Bill` → `C:\Users\User`), automatically rewrites all references to the old user path:
  - `.lnk` shortcut targets and working directories
  - `.url` files
  - Registry values containing the old user profile path
  - App config files (INI, XML, JSON) with hardcoded user paths
  - Pinned taskbar/Start Menu items
  - Recent file lists and MRU entries
  - Environment variables referencing the old path
- **Transport Layer** — Abstracts the data movement method:
  - External storage (USB drive, external HDD)
  - Network share (SMB/NAS path)
  - Direct WiFi (TCP with mDNS device discovery)
- **Job System** — Tracks migration jobs with detailed logging. Produces structured job reports (JSON) that can be stored locally on the flash drive or pushed to a NAS share. Data model designed for future central dashboard integration.
- **Profile/Template System** — Technicians save reusable migration presets (e.g., "Standard Office PC", "Developer Workstation"). Templates stored as JSON on the portable drive with the ability to pull shared templates from a company NAS path.

#### 2. Portable Application (`ZeroInstall.App`)
WPF desktop application — the primary technician interface. Runs directly from USB flash drive, no install needed. Provides:
- Source/destination machine selection ("Which computer is this?")
- Customizable checklist UI: technician picks exactly what to transfer per job
- Three-tier migration workflow with automatic fallback suggestions
- Real-time transfer progress with per-item status
- Job history and log viewer
- Profile/template management (local + NAS)

#### 3. Transfer Agent (`ZeroInstall.Agent`)
Lightweight component deployed to source or destination machine for WiFi-based transfers:
- **Portable mode** (default) — Standalone exe, technician runs it manually on both machines. No install. Runs only while transfer is active.
- **Service mode** (optional) — Installs as a Windows Service for long-running or unattended transfers. Survives reboots.
- Uses TCP for data transfer with mDNS/DNS-SD for automatic peer discovery on the local network.

#### 4. CLI (`ZeroInstall.CLI`)
Command-line interface for scripting, automation, and headless operation. Mirrors all App capabilities for batch/scripted migrations.

#### 5. WinPE Restore Environment (`ZeroInstall.WinPE`)
For restoring full disk clones (.img/.raw/.vhdx) to new hardware:
- **Custom WinPE ISO builder** — Generates bootable USB/ISO with the restore tool baked in. Includes a driver injection wizard so the technician can add drivers for the destination hardware (chipset, storage controllers, NIC) before building the ISO.
- **PXE boot support** — NAS serves WinPE over PXE. New PC boots from network, pulls image from NAS share — no USB needed.
- **Driver injection** — Both at ISO build time (bake drivers into the WinPE image) and at restore time (inject drivers into the restored OS so it boots on the new hardware). Supports importing driver packs from OEM sources or a shared NAS driver repository.

### Data Flow

```
Source PC                                          Destination PC
┌──────────────┐                                  ┌──────────────┐
│ Discovery     │                                  │              │
│ ├─ Apps       │    ┌─────────────────────┐       │ Replay       │
│ ├─ Settings   │───▶│ Transport Layer     │──────▶│ ├─ winget    │
│ ├─ Profiles   │    │  • USB/External     │       │ ├─ Registry  │
│ └─ Files      │    │  • Network/NAS      │       │ ├─ Files     │
│               │    │  • Direct WiFi      │       │ └─ Settings  │
│ OR: Full Clone│───▶│                     │──────▶│ OR: Restore  │
│   .img/.vhdx  │    └─────────────────────┘       │   via WinPE  │
└──────────────┘                                  └──────────────┘
```

### Migration Tier Strategy

The technician selects what to transfer via the checklist UI. For each application:

1. **Tier 1 — Package Reinstall:** Check if the app exists in winget/chocolatey. If yes, generate an install manifest. On the destination, perform a clean install, then overlay user-specific settings and data from the source capture.
2. **Tier 2 — Registry + File Capture:** If the app is not in any package repo, capture its registry footprint (installer keys, app-specific HKLM/HKCU entries), Program Files directory, and AppData folders. Replay these on the destination. Flag to the technician that this is a best-effort migration.
3. **Tier 3 — Full Disk Clone:** If per-app migration is insufficient and the customer needs exact application state, clone the entire source volume to .img/.raw/.vhdx. Restore via WinPE boot environment with driver injection for new hardware.

## Build Commands

```bash
# Restore dependencies
dotnet restore

# Build entire solution (Debug)
dotnet build

# Build specific project
dotnet build src/ZeroInstall.Core/

# Build Release
dotnet build -c Release

# Run the WPF app
dotnet run --project src/ZeroInstall.App/

# Run the CLI
dotnet run --project src/ZeroInstall.CLI/

# Run all tests
dotnet test

# Run specific test project
dotnet test tests/ZeroInstall.Core.Tests/

# Run a single test by name
dotnet test --filter "FullyQualifiedName~TestClassName.TestMethodName"

# Publish portable (self-contained, single file)
dotnet publish src/ZeroInstall.App/ -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## Key Design Decisions

- **Portable-first:** The main app must run from a USB flash drive with zero installation. All state (logs, profiles, temp data) is stored relative to the exe or on configured NAS paths — never in system-wide locations.
- **No internet required:** Core migration functionality works fully offline (local network or USB). Package-based reinstall (Tier 1) needs internet on the destination only.
- **Structured job logging:** Job reports use JSON format with a schema designed for future central dashboard consumption. Local-first storage with optional NAS push.
- **Template portability:** Migration profiles are JSON files stored alongside the portable app on the flash drive, with pull-from-NAS capability for org-wide shared templates.
- **Self-contained publish:** Release builds should be published as self-contained single-file executables so technicians don't need .NET installed on their machines or flash drives.
- **Transport abstraction:** The transport layer is abstracted so adding new transfer methods (e.g., Bluetooth, cloud relay) doesn't require changing the migration engine.

## Brand Colors (CCNNY)

All UI theming must use the company color palette from [ccnny.us](https://ccnny.us):

| Role              | Color     | Hex       |
|-------------------|-----------|-----------|
| Primary (teal)    | Teal      | `#1088A1` |
| Primary dark      | Deep teal | `#0E4D6C` |
| Accent (orange)   | Orange    | `#F58850` |
| Background        | White     | `#FFFFFF` |
| Surface/subtle    | Light gray| `#F5F5F5` |
| Border            | Gray      | `#E8E8E8` |
| Text primary      | Dark      | `#24282D` |
| Text secondary    | Gray      | `#333333` |

- Primary gradient for headers/buttons: `linear-gradient(135deg, #1088A1, #0E4D6C)`
- Orange for call-to-action buttons, progress indicators, and interactive highlights
- White/light gray for backgrounds — clean, professional look
- Define these as WPF resource dictionary colors in a shared `Themes/BrandColors.xaml` consumed by all UI projects

## Conventions

- Target `net8.0-windows` (or latest LTS) for all projects
- Use nullable reference types (`<Nullable>enable</Nullable>`) throughout
- Async/await for all I/O operations — migrations are long-running and must remain responsive
- CancellationToken support on all migration/transfer operations
- IProgress<T> pattern for reporting transfer progress to UI
- Dependency injection via `Microsoft.Extensions.DependencyInjection`
- Configuration via `Microsoft.Extensions.Configuration` (appsettings.json + environment)
- Logging via `Microsoft.Extensions.Logging` with Serilog sink
- xUnit for unit tests, FluentAssertions for assertions
