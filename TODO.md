# ZeroInstall Migrator — Master TODO

## Phase 0: Project Scaffolding ✅
- [x] Initialize git repo and push to GitHub
- [x] Create .NET solution file (`ZeroInstall.sln`)
- [x] Scaffold project structure (8 projects)
- [x] Configure shared build props (`Directory.Build.props`) — nullable, TFM, version
- [x] Add NuGet packages: Serilog, FluentAssertions, DI, Configuration
- [x] Create `Themes/BrandColors.xaml` resource dictionary with CCNNY palette
- [x] Add LICENSE file (GNU GPL v3)
- [x] Add .gitignore for .NET/Visual Studio
- [x] Create `profiles/` directory with sample template JSON schema

## Phase 1: Core Models & Interfaces ✅
- [x] Define core domain models (10): MigrationJob, DiscoveredApplication, UserProfile, SystemSetting, MigrationItem, MigrationProfile, TransferManifest, JobReport, TransferProgress, etc.
- [x] Define core interfaces (7): IDiscoveryService, IMigrator, IPackageMigrator, IRegistryMigrator, IDiskCloner, ITransport, IProfileManager, IJobLogger
- [x] Define enums (7): MigrationTier, TransportMethod, MigrationItemType, JobStatus, MigrationItemStatus, DiskImageFormat, PackageManagerType
- [x] Set up DI container registration in `ZeroInstall.Core`
- [x] Write unit tests for model validation and serialization (21 tests)

## Phase 2: Discovery Module ✅
- [x] Application Discovery (registry scan, winget/chocolatey cross-reference, portable app detection)
- [x] User Profile Discovery (profile enumeration, known folders, browser/email data detection)
- [x] System Settings Discovery (printers, WiFi, mapped drives, env vars, scheduled tasks, credentials, certificates)
- [x] Discovery Result Aggregation (MigrationItem checklist, tier tagging, size estimates)
- [x] Write tests (41 total)

## Phase 3: Transport Layer ✅
- [x] Define `ITransport` interface with streaming support
- [x] Common transport utilities: ChecksumHelper, CompressionHelper, StreamCopyHelper
- [x] External Storage Transport (USB/HDD with resumable transfers)
- [x] Network Share Transport (SMB/UNC with credentials)
- [x] Direct WiFi Transport (TCP with UDP peer discovery)
- [x] Write tests (39 new, 80 total)

## Phase 4: Tier 1 — Package-Based Migrator ✅
- [x] PackageMigratorService (resolve, capture, restore, install packages)
- [x] AppDataCaptureHelper (AppData dirs, HKCU registry, user path remapping)
- [x] Write tests (34 new, 114 total)

## Phase 5: Tier 2 — Registry + File Capture Migrator ✅
- [x] RegistryCaptureService (smart key builder, hardware filtering, path remapping)
- [x] FileCaptureService (Program Files, AppData, ProgramData, timestamp preservation)
- [x] RegistryFileMigratorService (orchestrator, Start Menu shortcuts, licensing/COM warnings)
- [x] Write tests (47 new, 161 total)

## Phase 6: Tier 3 — Full Disk Cloner ✅
- [x] DiskClonerService (VSS shadow, raw/vhdx clone, block copy, verify)
- [x] DiskImageMetadata (metadata model, chunk tracking, JSON persistence)
- [x] ImageSplitter (FAT32-aware splitting, reassembly, progress reporting)
- [x] Write tests (73 new, 234 total)

## Phase 7: Profile & Settings Migrator ✅
- [x] **Multi-User Selection & Mapping:**
  - [x] UserAccountService — create/check users, get SID/profile path
  - [x] UserMapping model — source→dest user mapping with path remapping detection
- [x] **User Profile Transfer:**
  - [x] ProfileTransferService — copy profile folders per user mapping
- [x] **User Path Remapping:**
  - [x] UserPathRemapService — rewrite .lnk, .url, registry, config files, env vars
- [x] **Browser Data:**
  - [x] BrowserDataService — Chrome, Firefox, Edge bookmarks/extensions/settings capture & restore
- [x] **Email Data (Phase 7.5):**
  - [x] EmailDataService — Outlook (PST/OST) + Thunderbird profile capture & restore
- [x] **System Settings Replay:**
  - [x] SystemSettingsReplayService — WiFi, printers, mapped drives, env vars, scheduled tasks, credentials, certificates, default apps
- [x] **ProfileSettingsMigratorService** — top-level orchestrator coordinating all sub-services
- [x] Write tests (115 new, 349 total)

## Phase 8a: CLI ✅
- [x] Implement CLI commands using `System.CommandLine` 2.0.0:
  - [x] `zim discover` — scan + JSON/table output
  - [x] `zim capture` — capture with tier/profile/volume/format options
  - [x] `zim restore` — restore with user mapping + create-users
  - [x] `zim profile list|show` — manage migration profiles
  - [x] `zim job list|show|export` — view job history and reports
- [x] JsonJobLogger + JsonProfileManager (portable path-based)
- [x] DI wiring via CliHost
- [x] Write tests (66 new, 415 total)

## Phase 8b: WPF App Foundation ✅
- [x] **App Shell:**
  - [x] MVVM infrastructure (CommunityToolkit.Mvvm, ViewModelBase, ObservableObject)
  - [x] INavigationService — ViewModel-first navigation with back/forward history
  - [x] AppHost — DI wiring with Serilog logging
  - [x] MainWindow — gradient header, step indicator, content area, footer nav
  - [x] BrandColors.xaml + shared styles (HeaderText, BodyText, AccentButton, etc.)
- [x] **Welcome View:**
  - [x] Source / Destination role selection cards
- [x] **Discovery View:**
  - [x] Scan status bar, grouped ListView with checkboxes, tier badges
  - [x] Select all / none, item count + size summary, Proceed button
  - [x] MigrationItemViewModel — wraps model with UI property change
- [x] **Converters:** BoolToVisibility, InverseBool, MigrationTierToBadge, BytesToHumanReadable
- [x] Write tests (50 new, 465 total)

## Phase 9: WPF Migration Workflow ✅
- [x] **Session State Service:**
  - [x] ISessionState / SessionState — singleton shared state (role, selected items, user mappings, transport, paths, current job)
  - [x] Reset method for starting new migrations
- [x] **Migration Coordinator:**
  - [x] IMigrationCoordinator / MigrationCoordinator — orchestrates capture/restore
  - [x] CaptureAsync — groups items by tier, calls PackageMigrator, RegistryMigrator, ProfileSettingsMigrator
  - [x] RestoreAsync — calls PackageMigrator, RegistryMigrator, ProfileSettingsMigrator with user mappings
  - [x] Job lifecycle management (create, update status, handle cancel/failure)
- [x] **Capture Config View (Source side):**
  - [x] Output path picker, transport method dropdown
  - [x] Summary cards (package/registry/profile item counts, total size)
  - [x] StartCapture → navigates to MigrationProgress
- [x] **Restore Config View (Destination side):**
  - [x] Input path picker, manifest loading, capture info display
  - [x] User mapping table (source→dest, create-if-missing checkbox)
  - [x] StartRestore → navigates to MigrationProgress
- [x] **Migration Progress View (shared):**
  - [x] Progress bar, speed/ETA, status text
  - [x] Per-item status list (queued/in-progress/completed/failed icons)
  - [x] Cancel button, error handling
  - [x] Auto-navigates to JobSummary on completion
- [x] **Job Summary View (shared):**
  - [x] Success/failed/warning count cards with color coding
  - [x] Duration display, overall status
  - [x] Per-item results list
  - [x] Export Report + New Migration buttons
- [x] **Converters:** MigrationItemStatusToIcon, PercentToWidth
- [x] **Wiring:**
  - [x] AppHost — registered SessionState, MigrationCoordinator, 4 new ViewModels
  - [x] MainWindow.xaml — 4 new DataTemplates
  - [x] MainWindowViewModel — 5 steps, step index mapping for all VMs
  - [x] WelcomeViewModel — ISessionState injection, Destination→RestoreConfig navigation
  - [x] DiscoveryViewModel — ISessionState + INavigationService, Proceed→CaptureConfig with item save
- [x] Write tests (60 new, 525 total)

## Phase 10: Transfer Agent (`zim-agent`) ✅
- [x] **Models:**
  - [x] AgentRole enum (Source/Destination)
  - [x] AgentOptions (role, port, shared key, mode, directory, peer address)
  - [x] AgentHandshake / AgentHandshakeResponse (JSON-serialized auth protocol)
- [x] **Agent Protocol:**
  - [x] AgentProtocol — sentinel-path-based handshake/completion over ITransport.SendAsync/ReceiveAsync
  - [x] Shared-key authentication (plaintext, local-network use)
- [x] **Agent Core:**
  - [x] IAgentTransferService / AgentTransferService — full source + destination transfer orchestration
  - [x] Source flow: UDP discovery responder, TCP listen, authenticate, enumerate files, build manifest, send all files with checksums
  - [x] Destination flow: discover/connect peer, authenticate, receive manifest, receive files by count, write to disk preserving relative paths
  - [x] ProgressChanged / StatusChanged events for UI
- [x] **Portable Mode:**
  - [x] AgentPortableService (IHostedService) — single transfer, then StopApplication()
  - [x] AgentConsoleUI — header banner, progress bar with speed/ETA, transfer summary
  - [x] Command-line args: `--role`, `--port`, `--key`, `--mode`, `--dir`, `--peer`
- [x] **Service Mode:**
  - [x] AgentWindowsService (BackgroundService) — loop: listen → authenticate → transfer → repeat
  - [x] ServiceInstaller — `sc.exe create/delete/query` via IProcessRunner
  - [x] `zim-agent install --key --port` / `zim-agent uninstall` subcommands
- [x] **Hosting:**
  - [x] AgentHost — DI host builder with Serilog, portable vs service mode switching, UseWindowsService()
  - [x] Program.cs — System.CommandLine 2.0.0 entry point with root command + install/uninstall subcommands
- [x] Write tests (47 new, 572 total) — models, protocol loopback, full transfer integration, service installer, console UI, host builder

## Phase 11: WinPE Restore Environment
- [ ] **WinPE ISO Builder:**
  - [ ] Script to build custom WinPE image using Windows ADK
  - [ ] Embed ZeroInstall restore tool into the WinPE image
  - [ ] Driver injection wizard (browse INFs, OEM packs, NAS driver repo)
  - [ ] Generate bootable ISO, write to USB
  - [ ] Support UEFI and Legacy BIOS boot
- [ ] **PXE Boot Server:**
  - [ ] Configuration guide/scripts for NAS-based PXE (TFTP + DHCP options)
  - [ ] WinPE image served via TFTP, boot menu
- [ ] **Restore Tool (runs inside WinPE):**
  - [ ] Minimal GUI or TUI for WinPE environment
  - [ ] Browse NAS for .img/.raw/.vhdx, select target disk
  - [ ] Apply image, post-restore driver injection (offline DISM)
  - [ ] Verify integrity, reboot
- [ ] Write tests for image application logic

## Phase 12: Testing & Quality
- [ ] Achieve unit test coverage for all Core services
- [ ] Integration tests for each transport method
- [ ] Integration tests for agent-to-agent transfer
- [ ] End-to-end test: discovery → package migration → settings overlay
- [ ] End-to-end test: full clone → WinPE restore
- [ ] Test on Windows 10 and Windows 11 (both Home and Pro)
- [ ] Test portable mode from USB flash drive (FAT32 and NTFS)
- [ ] Test with real-world app scenarios (Office, Chrome, QuickBooks)

## Phase 13: Packaging & Code Signing
- [ ] **Build pipeline:**
  - [ ] Publish self-contained single-file executables (win-x64)
  - [ ] Version stamping (semantic versioning)
- [ ] **Code signing:**
  - [ ] Obtain code signing certificate
  - [ ] Sign all executables and DLLs
- [ ] **Distribution package:**
  - [ ] Portable folder structure for USB deployment
  - [ ] ZIP archive, README with technician quickstart guide
- [ ] **GitHub Release:**
  - [ ] GitHub Actions CI/CD pipeline (build, test, publish)
  - [ ] Automated releases with signed artifacts, changelog

## Future Considerations (Post-v1.0)
- [ ] Central web dashboard for job tracking across all technicians
- [ ] Cloud relay transport (for remote migrations over internet)
- [ ] Bluetooth transport for nearby transfers
- [ ] macOS/Linux source support (read data from non-Windows drives)
- [ ] Active Directory / domain profile migration
- [ ] BitLocker-encrypted volume handling
- [ ] UEFI firmware settings backup/restore
