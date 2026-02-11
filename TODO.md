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

## Phase 11: WinPE Restore Environment ✅
- [x] **Core Services:**
  - [x] DiskInfo / VolumeDetail models for disk/volume enumeration
  - [x] DiskEnumerationService — PowerShell Get-Disk/Get-Volume with JSON parsing (handles single-object quirk)
  - [x] DriverInjectionService — DISM /Add-Driver wrapper with output parsing, .inf file discovery
  - [x] DI registration in ServiceCollectionExtensions
- [x] **WinPE Restore Tool (`zim-winpe`):**
  - [x] System.CommandLine 2.0.0 entry point with `--image`, `--target`, `--driver-path`, `--skip-verify`, `--no-confirm`, `--verbose`
  - [x] Interactive TUI mode (no args): image browse → metadata display → disk/volume selection → space validation → confirm → restore → driver injection → done
  - [x] Headless mode (`--image` + `--target`): scripted/automated restore
  - [x] WinPeHost — DI host builder with Serilog (file + console), Core + WinPE service registration
  - [x] WinPeConsoleUI — numbered menus, disk/volume tables, image metadata display, progress bar, color-coded status
  - [x] ImageBrowserService — recursive .img/.raw/.vhdx scan with .zim-meta.json metadata loading
  - [x] RestoreOrchestrator — verify → restore → driver injection workflow with error handling
  - [x] RestoreInteractiveCommand — full 12-step interactive TUI workflow
- [x] **WinPE ISO Builder:**
  - [x] Build-WinPE.ps1 — Windows ADK-based ISO builder with zim-winpe embedding, optional driver injection, UEFI + Legacy BIOS support
  - [x] Add-Drivers.ps1 — standalone WIM driver injection script
  - [x] tools/winpe/README.md — prerequisites and usage guide
- [x] **PXE Boot:**
  - [x] docs/PXE-Boot-Guide.md — TFTP/DHCP setup (WDS, dnsmasq, Serva), client config, troubleshooting
- [x] Write tests (54 new, 626 total) — models, JSON parsing, DISM output parsing, image browsing, restore orchestration, console UI, host builder

## Phase 12: WPF App Support Screens ✅
- [x] **Dialog Service:**
  - [x] IDialogService / DialogService — wraps OpenFolderDialog + OpenFileDialog for testability
- [x] **App Settings:**
  - [x] AppSettings model (NasPath, DefaultTransportMethod, DefaultLogLevel)
  - [x] IAppSettings / JsonAppSettings — JSON persistence at `{basePath}/config/settings.json`
- [x] **Transport Configuration:**
  - [x] TransportMethodToVisibilityConverter — show/hide panels by selected transport
  - [x] ISessionState / SessionState — 5 new transport properties (NetworkSharePath, Username, Password, DirectWiFiPort, SharedKey)
  - [x] CaptureConfigView — conditional Network Share + Direct WiFi config panels
- [x] **Dialog Wiring:**
  - [x] CaptureConfigViewModel — IDialogService for BrowseOutput, transport config save to session
  - [x] RestoreConfigViewModel — IDialogService for BrowseInput
- [x] **Profile Management:**
  - [x] ProfileListViewModel — list local/NAS profiles, create/edit/delete/load actions
  - [x] ProfileEditorViewModel — full profile editor with all section fields + save/cancel
  - [x] ProfileListView.xaml — dual-section list (local + NAS), toolbar
  - [x] ProfileEditorView.xaml — scrollable form with grouped checkbox sections
- [x] **Job History:**
  - [x] JobHistoryViewModel — load jobs, select for detail, export report via dialog
  - [x] JobHistoryView.xaml — split list + detail panel layout
- [x] **Settings Screen:**
  - [x] SettingsViewModel — NAS path, default transport, log level, save/cancel
  - [x] SettingsView.xaml — NAS config, defaults, about section
- [x] **Entry Points:**
  - [x] WelcomeViewModel — 3 new navigation commands (Profiles, Job History, Settings)
  - [x] WelcomeView.xaml — 3 auxiliary link buttons below role cards
  - [x] MainWindowViewModel — OpenSettings command
  - [x] MainWindow.xaml — 4 new DataTemplates, settings gear button in header
- [x] **DI Registration:**
  - [x] AppHost — IDialogService, IAppSettings, 4 new ViewModels, NAS-aware IProfileManager
- [x] Write tests (62 new, 688 total)

## Phase 13: Testing & Quality ✅
- [x] Achieve unit test coverage for all Core services (15 gap tests: JsonJobLogger, JsonProfileManager, DiskEnumeration, DriverInjection)
- [x] Integration tests for each transport method (6 tests: NetworkShare +3, DirectWiFi +3)
- [x] Integration tests for agent-to-agent transfer (2 tests: large file, source progress events)
- [x] End-to-end test: discovery → package migration → settings overlay (8 tests in CaptureRestorePipelineTests)
- [x] End-to-end test: full clone → WinPE restore (6 tests in DiskCloneRestorePipelineTests)
- [ ] Test on Windows 10 and Windows 11 (both Home and Pro)
- [ ] Test portable mode from USB flash drive (FAT32 and NTFS)
- [ ] Test with real-world app scenarios (Office, Chrome, QuickBooks)

## Phase 14: Packaging & Code Signing ✅
- [x] **Build pipeline:**
  - [x] Publish self-contained single-file executables (win-x64) — `tools/Publish-All.ps1`
  - [x] Version stamping (semantic versioning) — `tools/Set-Version.ps1`, `global.json`
- [x] **Code signing:**
  - [ ] Obtain code signing certificate (pending — infrastructure ready)
  - [x] Sign all executables and DLLs — `tools/Sign-Binaries.ps1` (infrastructure-only, ready for cert)
- [x] **Distribution package:**
  - [x] Portable folder structure for USB deployment (ZeroInstall/ with subfolders)
  - [x] ZIP archive, README with technician quickstart guide — `dist/README.md`
- [x] **GitHub Release:**
  - [x] GitHub Actions CI/CD pipeline (build, test, publish) — `.github/workflows/ci.yml`
  - [x] Automated releases with signed artifacts, changelog (on `v*` tags)

## Phase 15: SFTP Transport & Remote Backup/Restore ✅
- [x] **SFTP Transport:**
  - [x] New `SftpTransport` implementation (plugs into existing ITransport abstraction)
  - [x] SFTP connection management (host, port, username, key-based or password auth)
  - [x] ISftpClientWrapper / SftpClientWrapper — SSH.NET abstraction for testability
  - [x] Resumable uploads/downloads (track partial transfers via remote checksum resume log)
  - [x] Chunked uploads (256 MB chunks for 500 GB+ images)
  - [x] `.tmp` suffix during upload, rename on completion for atomicity
  - [x] Connection testing and diagnostics
- [x] **Compression Pipeline:**
  - [x] GZip streaming compression before upload (configurable on/off)
  - [x] Automatic decompression on download
- [x] **Encryption:**
  - [x] EncryptionHelper — AES-256-CBC streaming encryption with PBKDF2-SHA256 key derivation
  - [x] "ZIME" magic header format (4-byte magic + 16-byte salt + 16-byte IV + ciphertext)
  - [x] Per-job passphrase-based encryption (optional, enabled per backup)
  - [x] Encrypted manifest support (manifest encrypted/decrypted transparently)
- [x] **Remote Backup (Source side — at customer location):**
  - [x] Capture locally → compress → encrypt → upload to NAS via SFTP
  - [x] Progress reporting with per-chunk status
- [x] **Remote Restore (Destination side):**
  - [x] Download from NAS via SFTP → decrypt → decompress → restore
  - [x] Restore locally at shop or remotely at customer site
- [x] **CLI Integration:**
  - [x] `zim capture --sftp-host --sftp-port --sftp-user --sftp-pass --sftp-key --sftp-path --encrypt --no-compress`
  - [x] `zim restore --sftp-host --sftp-port --sftp-user --sftp-pass --sftp-key --sftp-path --encrypt --no-compress`
- [x] **WPF Integration:**
  - [x] SFTP config panel in CaptureConfig view (host, port, user, pass, SSH key, remote path, encryption, compress)
  - [x] SFTP config panel in RestoreConfig view
  - [x] NAS browser UI (connect, browse directories, create folders)
  - [x] SftpTransportConfiguration model, 9 SFTP properties on ISessionState/SessionState
- [x] **NAS Directory Structure:**
  - [x] Manifest file per backup with metadata (zim-manifest.json)
  - [x] Resume log per backup (zim-resume.json with checksums)
  - [x] Listing/browsing backups on the NAS from the app (ListRemoteDirectoryAsync)
- [x] Write tests (63 new, 788 total)

## Phase 16: Persistent Customer Backup Agent ✅
- [x] **Scheduled Backup Service:**
  - [x] Windows Service that runs permanently on customer PCs (BackupSchedulerService + BackupHost)
  - [x] Configurable backup schedule (daily, weekly, custom cron via NCrontab)
  - [x] File/folder backup mode (incremental - only changed files since last backup via BackupIndex diff)
  - [x] Full image backup mode (placeholder - delegates to CLI/GUI for now)
  - [x] Retention policy (keep last N backups, auto-delete old ones on NAS via RetentionService)
- [x] **Customer-Facing Lightweight UI:**
  - [x] System tray icon with backup status (BackupTrayIcon + BackupStatusForm)
  - [x] Simple config: what to back up, schedule, encryption on/off (BackupSettingsForm)
  - [x] Manual "Back up now" button (TriggerBackupNowAsync)
  - [x] Restore request workflow (customer initiates via tray, RestoreRequest uploaded to NAS)
- [x] **Technician Management:**
  - [x] Remote deployment of the agent to customer PCs (zim-backup install)
  - [x] Central config push (NAS-based config polling via ConfigSyncService)
  - [x] Alert/notification when a customer's backup fails or is overdue (StatusReporter uploads to NAS)
- [x] **Storage:**
  - [x] Company-provided NAS storage (SFTP transport, no cloud dependency)
  - [x] Per-customer storage quotas (configurable, enforced by BackupExecutor)
  - [x] Deduplication-friendly incremental backups (SHA-256 based file index diffing)
- [x] **Security:**
  - [x] All backups encrypted at rest on NAS (AES-256-CBC via EncryptionHelper)
  - [x] SFTP for all transfers (SSH.NET)
  - [x] Per-customer encryption keys (passphrase-based in BackupConfiguration)
- [x] Write tests (84 new, 872 total)

## Phase 18: Bluetooth Transport ✅
- [x] **Core Transport:**
  - [x] IBluetoothAdapter — testability abstraction (IsBluetoothAvailable, DiscoverDevicesAsync, PairAsync, ConnectAsync, AcceptConnectionAsync, LocalDeviceName)
  - [x] BluetoothAdapter — concrete impl wrapping InTheHand.Net.Bluetooth 4.2.x (32feet.NET)
  - [x] DiscoveredBluetoothDevice model (DeviceName, Address, AddressString, IsPaired, IsZimService)
  - [x] BluetoothTransport (ITransport + IAsyncDisposable) — RFCOMM, same 4-byte frame protocol as DirectWiFiTransport
  - [x] Static helpers: EstimateTransferTime, DiscoverDevicesAsync convenience wrapper
- [x] **Enum + SessionState:**
  - [x] TransportMethod.Bluetooth enum value
  - [x] ISessionState/SessionState — BluetoothDeviceName, BluetoothDeviceAddress, BluetoothIsServer + Reset()
- [x] **CLI Integration:**
  - [x] `zim capture --bt-address --bt-server` options
  - [x] `zim restore --bt-address --bt-server` options
- [x] **WPF Integration:**
  - [x] NonEmptyStringToVisibilityConverter
  - [x] CaptureConfigViewModel — Bluetooth properties, scan/pair commands, TransportMethods now has 5 entries
  - [x] RestoreConfigViewModel — Bluetooth properties (BluetoothIsServer defaults true for restore/listening)
  - [x] CaptureConfigView.xaml — Bluetooth config panel (speed warning, server/client radio, device list, pair button)
  - [x] RestoreConfigView.xaml — Bluetooth config panel
- [x] Write tests (56 new, 1007 total)

## Phase 19: UEFI Firmware Settings Backup/Restore ✅
- [x] **Core Enums & Models:**
  - [x] FirmwareType enum (Unknown, Bios, Uefi)
  - [x] SecureBootStatus enum (Unknown, Enabled, Disabled, NotSupported)
  - [x] FirmwareInfo model (firmware type, Secure Boot, TPM, BIOS vendor/version, system manufacturer/model, boot entries)
  - [x] BcdBootEntry model (identifier, entry type, description, device, path, default flag, properties dict)
- [x] **FirmwareService:**
  - [x] IFirmwareService interface (GetFirmwareInfoAsync, ExportBcdAsync, ImportBcdAsync, GetBootEntriesAsync)
  - [x] FirmwareService (internal) — PowerShell WMI queries + bcdedit via IProcessRunner
  - [x] Static parse methods: ParseFirmwareType, ParseSecureBootStatus, ParseBiosInfo, ParseSystemInfo, ParseTpmInfo, ParseBcdEnum
  - [x] DI registration in ServiceCollectionExtensions
- [x] **CLI Integration:**
  - [x] FirmwareCommand with 4 subcommands (status, backup-bcd, restore-bcd, list-boot-entries)
  - [x] OutputFormatter.WriteFirmwareInfo and WriteBootEntries methods
  - [x] Program.cs wiring
- [x] **WPF Integration:**
  - [x] ISessionState/SessionState — IncludeBcdBackup property (default: true) + Reset()
  - [x] CaptureConfigViewModel — optional IFirmwareService?, firmware display properties, CheckFirmwareInfoAsync
  - [x] CaptureConfigView.xaml — firmware info panel with diagnostics, BCD checkbox, amber warning about non-portable settings
- [x] Write tests (51 new, 1058 total)

## Phase 20: Domain Migration (ForensIT-Grade) ✅
- [x] **Core Enums & Models:**
  - [x] DomainJoinType enum (Unknown, Workgroup, ActiveDirectory, AzureAd, HybridAzureAd)
  - [x] UserAccountType enum (Unknown, Local, ActiveDirectory, AzureAd, MicrosoftAccount)
  - [x] PostMigrationAccountAction enum (None, Disable, Delete)
  - [x] DomainInfo model (JoinType, DomainOrWorkgroup, IsDomainJoined, AzureAd tenant fields, DomainController)
  - [x] DomainCredentials model (Domain, Username, Password [JsonIgnore], IsValid)
  - [x] DomainMigrationConfiguration model (TargetDomain, TargetOu, ComputerNewName, credentials, UserLookupMap, PostMigrationScript, PostMigrationAccountAction)
  - [x] UserProfile extended with DomainName, AccountType
  - [x] UserMapping extended with DomainMigrationWarning, PostMigrationAction, ReassignInPlace
  - [x] MigrationJob extended with SourceDomainInfo, DestinationDomainInfo, DomainMigrationConfig
- [x] **DomainService:**
  - [x] IDomainService interface (GetDomainInfoAsync, ClassifyUserAccountAsync, GetUserDomainAsync)
  - [x] DomainService (internal) — PowerShell WMI + dsregcmd + nltest + SID translation
  - [x] Static parse methods: ParseWmiDomainInfo, ParseDsregcmd, ParseNltest, ParseNtAccount
  - [x] DI registration in ServiceCollectionExtensions
- [x] **DomainJoinService:**
  - [x] IDomainJoinService interface (JoinDomainAsync, UnjoinDomainAsync, JoinAzureAdAsync, RenameComputerAsync)
  - [x] DomainJoinService (internal) — PowerShell Add-Computer/Remove-Computer/Rename-Computer, dsregcmd
  - [x] DI registration
- [x] **ProfileReassignmentService:**
  - [x] IProfileReassignmentService interface (ReassignProfileAsync, RenameProfileFolderAsync, SetSidHistoryAsync)
  - [x] ProfileReassignmentService (internal) — registry ProfileList surgery + icacls + NTUSER.DAT SID rewrite
  - [x] DI registration
- [x] **UserAccountManager extensions:**
  - [x] DeleteUserAsync, DisableUserAsync, SetAutoLogonAsync added to IUserAccountManager + UserAccountService
  - [x] IRegistryAccessor extended with SetStringValue
- [x] **UserProfileDiscoveryService:**
  - [x] Optional IDomainService? 4th param for proper account classification
  - [x] Fixed IsLocal heuristic (was broken `!sid.Contains("-500")`)
- [x] **CLI Integration:**
  - [x] DomainCommand with 5 subcommands (status, join, unjoin, rename, migrate-profile)
  - [x] OutputFormatter.WriteDomainInfo and WriteDomainJoinResult methods
  - [x] Program.cs wiring
- [x] **WPF Integration:**
  - [x] ISessionState/SessionState — DomainMigrationConfiguration? + Reset()
  - [x] CaptureConfigViewModel — optional IDomainService?, domain info panel, amber warning for AD
  - [x] CaptureConfigView.xaml — domain info panel with join type, domain/workgroup, DC display
  - [x] RestoreConfigViewModel — domain join config (target domain, OU, computer name, credentials, Azure AD)
  - [x] RestoreConfigView.xaml — domain configuration section with join options
  - [x] UserMappingEntryViewModel — DomainWarning, ShowDomainWarning, PostMigrationAction, ReassignInPlace, ShowDomainOptions
- [x] Write tests (94 new, 1152 total)

## Phase 21: Cross-Platform Source Discovery ✅
- [x] SourcePlatform enum (Unknown, Windows, MacOs, Linux)
- [x] IPlatformDetectionService + PlatformDetectionService (filesystem marker detection)
- [x] IFileSystemAccessor: ReadAllText + ReadAllLines extensions
- [x] DiscoveredApplication: BrewCaskId, AptPackageName, SnapPackageName, FlatpakAppId
- [x] MacOsUserProfileDiscoveryService (/Users/ enumeration, Safari/Chrome/Firefox/Edge, Outlook/Thunderbird/Apple Mail)
- [x] MacOsApplicationDiscoveryService (/Applications/ .app bundles, Info.plist parsing, Homebrew Cellar + Caskroom)
- [x] LinuxUserProfileDiscoveryService (/etc/passwd parsing, XDG folders, Chrome/Firefox/Chromium, Thunderbird/Evolution)
- [x] LinuxApplicationDiscoveryService (.desktop files, dpkg/status, snap, flatpak)
- [x] ICrossPlatformDiscoveryService + CrossPlatformDiscoveryService (orchestrator)
- [x] DI registration (IPlatformDetectionService + ICrossPlatformDiscoveryService)
- [x] CLI: --source-path option on discover + capture commands
- [x] CLI: OutputFormatter.WritePlatformInfo
- [x] CLI: Cross-platform tier validation (Tier 2/3 blocked for non-Windows sources)
- [x] WPF: Source path picker, platform detection banner (teal), limitation warning (amber)
- [x] WPF: CaptureConfigViewModel + SessionState source path support
- [x] Write tests (95 new, 1247 total)

## Future Considerations (Post-v1.0)
- [ ] Central web dashboard for job tracking and backup monitoring across all technicians/customers
- [x] macOS/Linux source support (Phase 21: cross-platform discovery, 95 new tests — 1247 total)
- [x] Active Directory / domain profile migration (Phase 20: ForensIT-grade domain migration, 94 new tests — 1152 total)
- [x] BitLocker-encrypted volume handling (Phase 17: enum, model, service, CLI commands, WPF warning, 79 new tests — 951 total)
- [x] Bluetooth transport for nearby transfers (Phase 18: IBluetoothAdapter, BluetoothTransport, CLI + WPF, 56 new tests — 1007 total)
- [x] UEFI firmware settings backup/restore (Phase 19: FirmwareService, BCD backup/restore, CLI + WPF, 51 new tests — 1058 total)
