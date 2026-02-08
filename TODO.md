# ZeroInstall Migrator — Master TODO

## Phase 0: Project Scaffolding
- [ ] Initialize git repo and push to GitHub
- [ ] Create .NET solution file (`ZeroInstall.sln`)
- [ ] Scaffold project structure:
  - [ ] `src/ZeroInstall.Core/` — Class library (net8.0-windows)
  - [ ] `src/ZeroInstall.App/` — WPF application
  - [ ] `src/ZeroInstall.Agent/` — Worker/console app
  - [ ] `src/ZeroInstall.CLI/` — Console app
  - [ ] `src/ZeroInstall.WinPE/` — Console app / tooling
  - [ ] `tests/ZeroInstall.Core.Tests/` — xUnit test project
  - [ ] `tests/ZeroInstall.App.Tests/` — xUnit test project
  - [ ] `tests/ZeroInstall.Agent.Tests/` — xUnit test project
- [ ] Configure shared build props (`Directory.Build.props`) — nullable, TFM, version
- [ ] Add NuGet packages: Serilog, FluentAssertions, DI, Configuration
- [ ] Create `Themes/BrandColors.xaml` resource dictionary with CCNNY palette
- [ ] Add LICENSE file (GNU GPL v3)
- [ ] Add .gitignore for .NET/Visual Studio
- [ ] Create `profiles/` directory with a sample template JSON schema

## Phase 1: Core Models & Interfaces
- [ ] Define core domain models:
  - [ ] `MigrationJob` — Represents a full migration session (source, dest, selected items, status, logs)
  - [ ] `DiscoveredApplication` — App found on source (name, version, install path, registry keys, package ID if available)
  - [ ] `UserProfile` — User account with associated data paths
  - [ ] `SystemSetting` — Transferable setting (printers, WiFi, env vars, mapped drives, etc.)
  - [ ] `MigrationItem` — A selectable item in the checklist (app, profile, setting, file group)
  - [ ] `MigrationProfile` — Saved template with pre-selected items and config
  - [ ] `TransferManifest` — What's being transferred, method, progress tracking
  - [ ] `JobReport` — Structured JSON report of a completed migration
- [ ] Define core interfaces:
  - [ ] `IDiscoveryService` — Scans source machine
  - [ ] `IMigrator` — Base interface for all migration tiers
  - [ ] `IPackageMigrator : IMigrator` — Tier 1
  - [ ] `IRegistryMigrator : IMigrator` — Tier 2
  - [ ] `IDiskCloner : IMigrator` — Tier 3
  - [ ] `ITransport` — Abstracts USB/NAS/WiFi data movement
  - [ ] `IProfileManager` — Load/save migration templates
  - [ ] `IJobLogger` — Structured job logging
- [ ] Define enums:
  - [ ] `MigrationTier` — Package, RegistryFile, FullClone
  - [ ] `TransportMethod` — ExternalStorage, NetworkShare, DirectWiFi
  - [ ] `MigrationItemType` — Application, UserProfile, SystemSetting, FileGroup
  - [ ] `JobStatus` — Pending, InProgress, Completed, Failed, PartialSuccess
- [ ] Set up DI container registration in `ZeroInstall.Core`
- [ ] Write unit tests for model validation and serialization

## Phase 2: Discovery Module
- [ ] **Application Discovery:**
  - [ ] Scan `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall` (64-bit)
  - [ ] Scan `HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall` (32-bit)
  - [ ] Scan `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall` (per-user installs)
  - [ ] Parse display name, version, install location, publisher, uninstall string
  - [ ] Cross-reference against winget source (`winget list` or winget COM API)
  - [ ] Cross-reference against chocolatey (`choco list --local-only`)
  - [ ] Identify portable apps in common locations (Desktop, Downloads, custom folders)
  - [ ] Calculate estimated size per application (Program Files + AppData)
- [ ] **User Profile Discovery:**
  - [ ] Enumerate local user profiles via `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList`
  - [ ] Map known folders: Documents, Desktop, Downloads, Pictures, Music, Videos
  - [ ] Detect browser profiles (Chrome, Firefox, Edge — AppData paths)
  - [ ] Detect email client data (Outlook PST/OST paths, Thunderbird profiles)
  - [ ] Calculate per-profile size
- [ ] **System Settings Discovery:**
  - [ ] Installed printers (registry + driver info)
  - [ ] WiFi saved networks (`netsh wlan export profile`)
  - [ ] Mapped network drives (per-user registry)
  - [ ] Environment variables (system + user)
  - [ ] Scheduled tasks (Task Scheduler COM API)
  - [ ] Windows Credential Manager entries
  - [ ] Installed certificates (user + machine stores)
  - [ ] Default app associations
- [ ] **Discovery Result Aggregation:**
  - [ ] Build `MigrationItem` checklist from all discovered items
  - [ ] Tag each item with recommended migration tier
  - [ ] Provide size estimates and time predictions
- [ ] Write comprehensive tests with mock registry/filesystem data

## Phase 3: Transport Layer ✅
- [x] Define `ITransport` interface with streaming support:
  - [x] `SendAsync`, `ReceiveAsync`, `TestConnectionAsync`, `SendManifestAsync`, `ReceiveManifestAsync`
- [x] **Common transport utilities:**
  - [x] `ChecksumHelper` — SHA-256 compute/verify for streams, files, byte arrays
  - [x] `CompressionHelper` — GZip compress/decompress for streams and byte arrays
  - [x] `StreamCopyHelper` — Progress-reporting stream copy with bandwidth throttling
- [x] **External Storage Transport:**
  - [x] Write to/read from USB drive or external HDD path
  - [x] Support resumable transfers (resume log with per-file checksum tracking)
  - [x] Drive detection (`GetAvailableDrives`) and free space validation (`HasSufficientSpace`)
  - [x] Cleanup support
- [x] **Network Share Transport:**
  - [x] Write to/read from SMB/UNC path (NAS share)
  - [x] Optional credential support (`NetworkCredential`)
  - [x] Resumable transfers with checksum skip
- [x] **Direct WiFi Transport:**
  - [x] TCP socket-based transfer between two machines (server/client model)
  - [x] UDP broadcast peer discovery (`DiscoverPeersAsync` / `RespondToDiscoveryAsync`)
  - [x] Length-prefixed frame protocol for reliable messaging
  - [x] Bandwidth throttling option
- [x] Write tests for all transport utilities and implementations (39 new tests, 80 total)

## Phase 4: Tier 1 — Package-Based Migrator ✅
- [x] **PackageMigratorService** (`src/ZeroInstall.Core/Migration/PackageMigratorService.cs`):
  - [x] `ResolvePackagesAsync` — maps discovered apps to winget/choco package IDs (prefers winget)
  - [x] `CaptureAsync` — captures AppData + registry settings for selected Package-tier apps
  - [x] `RestoreAsync` — installs packages then overlays captured settings with user path remapping
  - [x] `InstallPackagesAsync` — runs `winget install` / `choco install` per package
  - [x] Package manager availability detection with fallback (winget→choco)
  - [x] Per-app MigrationItemStatus tracking (InProgress/Completed/Failed)
  - [x] Progress reporting via `IProgress<TransferProgress>`
  - [x] Cancellation support
- [x] **AppDataCaptureHelper** (`src/ZeroInstall.Core/Migration/AppDataCaptureHelper.cs`):
  - [x] Captures AppData directories (Local, Roaming, LocalLow) for each app
  - [x] Exports HKCU registry keys (`reg export`) for per-user app settings
  - [x] Restores captured files with user path remapping (Bill→William)
  - [x] Imports registry exports (`reg import`) on destination
  - [x] AppDataCaptureManifest serialization for tracking captured data
- [x] Write tests with mock package manager responses (34 new tests, 114 total)

## Phase 5: Tier 2 — Registry + File Capture Migrator ✅
- [x] **RegistryCaptureService** (`src/ZeroInstall.Core/Migration/RegistryCaptureService.cs`):
  - [x] Export app-specific HKLM + HKCU keys via `reg.exe export`
  - [x] Smart key list builder: uninstall key, `SOFTWARE\{Publisher}`, `SOFTWARE\{Name}`, WOW6432Node for 32-bit, publisher\name combos, additional paths
  - [x] Hardware-specific key filtering (SYSTEM\Enum, MountedDevices, HARDWARE\, WindowsUpdate, etc.)
  - [x] User path remapping in .reg file contents (double-backslash format)
  - [x] Import with `reg.exe import`, temp file cleanup
- [x] **FileCaptureService** (`src/ZeroInstall.Core/Migration/FileCaptureService.cs`):
  - [x] Copy Program Files install locations
  - [x] Copy AppData (Local, Roaming, LocalLow) directories with category detection
  - [x] Copy ProgramData directories (by publisher/name lookup)
  - [x] Preserve file timestamps (creation, write, access)
  - [x] Handle locked/inaccessible files gracefully (log and skip)
  - [x] Restore with user path remapping for AppData paths
- [x] **RegistryFileMigratorService** (`src/ZeroInstall.Core/Migration/RegistryFileMigratorService.cs`):
  - [x] Orchestrates RegistryCaptureService + FileCaptureService for full Tier 2
  - [x] Creates Start Menu shortcuts (PowerShell WScript.Shell) in "ZeroInstall Migrated" folder
  - [x] Main executable detection (name matching, non-main filtering, size heuristic)
  - [x] Licensing warning detection (Adobe, Microsoft Office, JetBrains, etc.)
  - [x] COM registration detection (Microsoft, Adobe, Autodesk, Corel)
  - [x] Tier2CaptureManifest with warnings metadata
- [x] Write tests (47 new tests, 161 total)

## Phase 6: Tier 3 — Full Disk Cloner ✅
- [x] **DiskClonerService** (`src/ZeroInstall.Core/Migration/DiskClonerService.cs`):
  - [x] `CloneVolumeAsync` — captures volume to .img/.raw/.vhdx with VSS shadow copy support
  - [x] `CloneToRawImageAsync` — PowerShell raw device read with fallback to block copy
  - [x] `CloneToVhdxAsync` — Hyper-V New-VHD + Mount-VHD + robocopy approach with raw fallback
  - [x] `RestoreImageAsync` — restores image with chunk reassembly if split
  - [x] `RestoreFromRawImageAsync` / `RestoreFromVhdxAsync` — format-specific restore
  - [x] `VerifyImageAsync` — SHA-256 verification for single files and split chunks
  - [x] `CreateVssShadowAsync` / `DeleteVssShadowAsync` — VSS via vssadmin for live capture
  - [x] `BlockCopyAsync` — 1 MB block copy with speed/ETA progress reporting
  - [x] `CaptureAsync` / `RestoreAsync` — IMigrator implementation wrapping volume clone
  - [x] Per-item MigrationItemStatus tracking
- [x] **DiskImageMetadata** (`src/ZeroInstall.Core/Migration/DiskImageMetadata.cs`):
  - [x] Full metadata model: hostname, OS version, volume, size, format, checksum, VSS, filesystem
  - [x] Chunk tracking: IsSplit, ChunkCount, ChunkSizeBytes, ChunkChecksums
  - [x] `GetExtension`, `GetMetadataPath`, `GetChunkPath` static helpers
  - [x] `SaveAsync` / `LoadAsync` JSON persistence alongside image files
- [x] **ImageSplitter** (`src/ZeroInstall.Core/Migration/ImageSplitter.cs`):
  - [x] FAT32-aware chunk splitting (4 GB - 4096 default chunk size)
  - [x] `NeedsSplitting` / `CalculateChunkCount` — sizing utilities
  - [x] `SplitAsync` — splits file into numbered chunks with progress reporting
  - [x] `ReassembleAsync` — reassembles chunks into single file with progress reporting
  - [x] `IsFat32` — filesystem detection via DriveInfo
- [x] Write tests (73 new tests, 234 total)

## Phase 7: Profile & Settings Migrator
- [ ] **Multi-User Selection & Mapping:**
  - [ ] Present all discovered user profiles to technician for selection (some or all)
  - [ ] For each selected source user, map to an existing destination user OR create new
  - [ ] Account creation wizard: create local user on destination with technician-specified username/password
  - [ ] Support different username on destination (e.g., source "Bill" → dest "William")
  - [ ] Handle multiple independent user mappings per migration job
- [ ] **User Profile Transfer:**
  - [ ] Copy user profile folders per the user mapping (selective or complete)
  - [ ] Map SIDs between source and destination users
  - [ ] Fix NTFS permissions after copy (re-ACL for new SIDs)
- [ ] **User Path Remapping (per mapped user):**
  - [ ] Rewrite `.lnk` shortcut targets and working directories (e.g., `C:\Users\Bill\Desktop` → `C:\Users\William\Desktop`)
  - [ ] Rewrite `.url` file paths
  - [ ] Rewrite registry values containing old user profile paths (HKCU)
  - [ ] Scan and rewrite app config files (INI, XML, JSON) with hardcoded user paths
  - [ ] Fix pinned taskbar and Start Menu items
  - [ ] Fix Recent file lists and MRU registry entries
  - [ ] Update environment variables referencing old user paths
- [ ] **Browser Data:**
  - [ ] Chrome: bookmarks, extensions list, saved passwords (encrypted — export/import)
  - [ ] Firefox: profile folder copy (complete migration)
  - [ ] Edge: bookmarks, extensions, settings
- [ ] **System Settings Replay:**
  - [ ] Import WiFi profiles (`netsh wlan add profile`)
  - [ ] Add printers (install drivers + add printer connections)
  - [ ] Restore mapped network drives
  - [ ] Set environment variables
  - [ ] Import scheduled tasks
  - [ ] Import credentials to Credential Manager
  - [ ] Import certificates to appropriate stores
  - [ ] Restore default app associations
- [ ] Write tests for each setting category

## Phase 8: WPF Application (Technician GUI)
- [ ] **App Shell:**
  - [ ] Apply CCNNY brand theme (BrandColors.xaml, styles, control templates)
  - [ ] Navigation framework (page-based or wizard-style flow)
  - [ ] Responsive layout that works at various resolutions
  - [ ] Status bar with connection status, job progress
- [ ] **Welcome / Role Selection Page:**
  - [ ] "Which computer is this?" — Source or Destination
  - [ ] Transport method selection (USB, Network, WiFi)
  - [ ] Connection setup (path entry, peer discovery, etc.)
- [ ] **Discovery Results Page (Source):**
  - [ ] Treeview/checklist of all discovered items grouped by category
  - [ ] Select all / deselect all per category
  - [ ] Size estimates per item and total
  - [ ] Migration tier indicator per app (Tier 1/2/3 badge)
  - [ ] Search/filter within discovered items
- [ ] **Migration Progress Page:**
  - [ ] Overall progress bar with percentage and ETA
  - [ ] Per-item progress list (queued → in progress → done/failed)
  - [ ] Real-time log viewer (collapsible)
  - [ ] Cancel button with confirmation
  - [ ] Pause/resume support
- [ ] **Job Summary Page:**
  - [ ] Success/failure count per category
  - [ ] Items that need manual attention (flagged)
  - [ ] Export job report (JSON + human-readable)
  - [ ] Option to save as migration profile template
- [ ] **Profile/Template Manager:**
  - [ ] List saved profiles (local flash drive)
  - [ ] Pull profiles from NAS path
  - [ ] Create/edit/delete profiles
  - [ ] Apply profile to pre-select checklist items
- [ ] **Settings Page:**
  - [ ] NAS path configuration
  - [ ] Default transport method
  - [ ] Log verbosity level
  - [ ] NAS push for job reports (enable/disable + path)
- [ ] Write UI tests (ViewModel unit tests with mock services)

## Phase 9: Transfer Agent
- [ ] **Agent Core:**
  - [ ] TCP listener for incoming transfer connections
  - [ ] mDNS/DNS-SD service advertisement and discovery
  - [ ] Authentication handshake (simple shared key or challenge)
  - [ ] File receive pipeline (stream to disk with progress)
  - [ ] File send pipeline (disk to stream with progress)
- [ ] **Portable Mode:**
  - [ ] Single exe, no install needed
  - [ ] Console UI showing connection status and transfer progress
  - [ ] Auto-exit when transfer completes
  - [ ] Command-line args: role (source/dest), port, key
- [ ] **Service Mode:**
  - [ ] Windows Service wrapper (`IHostedService`)
  - [ ] Install/uninstall via CLI (`ZeroInstall.Agent --install` / `--uninstall`)
  - [ ] Auto-start on boot
  - [ ] System tray icon for status (optional)
- [ ] Write integration tests for agent-to-agent communication

## Phase 10: CLI
- [ ] Implement CLI commands using `System.CommandLine`:
  - [ ] `zim discover` — Run discovery on current machine, output JSON or table
  - [ ] `zim migrate` — Start migration with options (transport, tier, items)
  - [ ] `zim clone` — Create disk clone (.img/.raw/.vhdx)
  - [ ] `zim restore` — Restore disk clone to target volume
  - [ ] `zim agent` — Start transfer agent (portable mode)
  - [ ] `zim profile list|apply|create|delete` — Manage migration profiles
  - [ ] `zim job list|view|export` — View job history and reports
- [ ] Structured output (JSON, table, or plain text via `--output` flag)
- [ ] Progress bars for long-running operations
- [ ] Exit codes for scripting
- [ ] Write tests for CLI argument parsing and command execution

## Phase 11: Job System & Logging
- [ ] **Job Persistence:**
  - [ ] SQLite database (portable, stored alongside exe) for job history
  - [ ] Job CRUD operations
  - [ ] Query/filter jobs by date, status, source/destination
- [ ] **Structured Logging:**
  - [ ] Serilog configuration: file sink (per-job log file) + console sink
  - [ ] Log levels: migration events, warnings (best-effort items), errors
  - [ ] Machine-readable JSON log format
- [ ] **Job Reports:**
  - [ ] Generate JSON report on job completion
  - [ ] Include: items attempted, succeeded, failed, skipped, warnings, duration
  - [ ] Local storage (flash drive `logs/` directory)
  - [ ] Optional push to NAS share path
  - [ ] Schema designed for future dashboard API consumption
- [ ] Write tests for job persistence and report generation

## Phase 12: Profile/Template System
- [ ] **Template Schema:**
  - [ ] JSON schema for migration profiles
  - [ ] Selected item categories, specific inclusions/exclusions
  - [ ] Transport preferences, tier preferences
  - [ ] Metadata: name, description, author, created/modified dates
- [ ] **Template Storage:**
  - [ ] Local: `profiles/` directory next to the portable exe on flash drive
  - [ ] NAS: configurable UNC path for shared org-wide templates
  - [ ] Merge strategy when local and NAS have same-named profiles
- [ ] **Default Templates:**
  - [ ] "Standard Office PC" — Office apps, browser, email, user files, printers
  - [ ] "Full Migration" — Everything discovered
  - [ ] "Files Only" — User profiles and documents, no apps
- [ ] Write tests for template CRUD and merge logic

## Phase 13: WinPE Restore Environment
- [ ] **WinPE ISO Builder:**
  - [ ] Script to build custom WinPE image using Windows ADK
  - [ ] Embed ZeroInstall restore tool into the WinPE image
  - [ ] **Driver injection wizard:**
    - [ ] Browse/select driver INFs or driver packs (.cab)
    - [ ] Import from OEM driver download (Dell, HP, Lenovo driver packs)
    - [ ] Pull drivers from shared NAS driver repository
    - [ ] Inject selected drivers into WinPE image (boot-critical: NIC, storage)
  - [ ] Generate bootable ISO file
  - [ ] Write ISO to USB flash drive (bootable)
  - [ ] Support UEFI and Legacy BIOS boot
- [ ] **PXE Boot Server:**
  - [ ] Configuration guide/scripts for NAS-based PXE (TFTP + DHCP options)
  - [ ] WinPE image served via TFTP
  - [ ] Boot menu with image selection
- [ ] **Restore Tool (runs inside WinPE):**
  - [ ] Minimal GUI or TUI for WinPE environment
  - [ ] Browse NAS share for available .img/.raw/.vhdx files
  - [ ] Select target disk and partition scheme
  - [ ] Apply image to disk (block-level for .img/.raw, mount+copy for .vhdx)
  - [ ] **Post-restore driver injection:**
    - [ ] Inject drivers into the restored OS (offline DISM)
    - [ ] Auto-detect new hardware and match drivers from driver pack
    - [ ] Fix boot-critical drivers (storage controller, NIC) to ensure OS boots
  - [ ] Verify restored image integrity
  - [ ] Reboot into restored OS
- [ ] Write tests for image application logic

## Phase 14: Testing & Quality
- [ ] Achieve unit test coverage for all Core services
- [ ] Integration tests for each transport method
- [ ] Integration tests for agent-to-agent transfer
- [ ] End-to-end test: discovery → package migration → settings overlay
- [ ] End-to-end test: full clone → WinPE restore
- [ ] Test on Windows 10 and Windows 11 (both Home and Pro)
- [ ] Test portable mode from USB flash drive (FAT32 and NTFS)
- [ ] Test with real-world app scenarios (Office, Chrome, QuickBooks)

## Phase 15: Packaging & Code Signing
- [ ] **Build pipeline:**
  - [ ] Publish self-contained single-file executables (win-x64)
  - [ ] Build all projects: App, Agent, CLI, WinPE tools
  - [ ] Version stamping (semantic versioning)
- [ ] **Code signing:**
  - [ ] Obtain code signing certificate (or self-signed for internal use)
  - [ ] Sign all executables and DLLs
  - [ ] Sign WinPE ISO if applicable
- [ ] **Distribution package:**
  - [ ] Create a portable folder structure for USB deployment:
    ```
    ZeroInstall-Migrator/
    ├── ZeroInstallMigrator.exe       # Main WPF app
    ├── ZeroInstallAgent.exe          # Transfer agent
    ├── zim.exe                       # CLI tool
    ├── profiles/                     # Migration templates
    ├── drivers/                      # Driver cache (optional)
    ├── logs/                         # Job logs and reports
    └── config/                       # Settings (NAS paths, preferences)
    ```
  - [ ] ZIP archive for download/distribution
  - [ ] README with technician quickstart guide
- [ ] **GitHub Release:**
  - [ ] GitHub Actions CI/CD pipeline (build, test, publish)
  - [ ] Automated GitHub Releases with signed artifacts
  - [ ] Changelog generation

## Future Considerations (Post-v1.0)
- [ ] Central web dashboard for job tracking across all technicians
- [ ] Cloud relay transport (for remote migrations over internet)
- [ ] Bluetooth transport for nearby transfers
- [ ] macOS/Linux source support (read data from non-Windows drives)
- [ ] Active Directory / domain profile migration
- [ ] BitLocker-encrypted volume handling
- [ ] UEFI firmware settings backup/restore
