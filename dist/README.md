# ZeroInstall Migrator — Technician Quick Start

## What's in this folder

| File / Folder | Description |
|---|---|
| `ZeroInstall.App.exe` | Main GUI application — start here |
| `zim.exe` | Command-line interface for scripting |
| `zim-agent.exe` | Transfer agent for WiFi-based transfers |
| `zim-backup.exe` | Persistent backup agent for customer PCs |
| `winpe/` | WinPE restore tool and ISO builder |
| `profiles/` | Migration profile templates |
| `docs/` | CLI reference and PXE boot guide |
| `config/` | Settings (created on first run) |
| `jobs/` | Migration job logs (created on first run) |

## Quick Start (GUI)

**On the source PC (old computer):**

1. Run `ZeroInstall.App.exe`
2. Select **"This is the SOURCE computer"**
3. The app scans for applications, user profiles, and settings
4. Check/uncheck items to migrate, then click **Proceed**
5. Choose an output location (this USB drive, network share, or WiFi)
6. Wait for capture to complete

**On the destination PC (new computer):**

7. Run `ZeroInstall.App.exe`
8. Select **"This is the DESTINATION computer"**
9. Point to the captured data
10. Map source users to destination users
11. Click **Start Restore**

## Quick Start (CLI)

```
# On source PC: scan and capture
zim discover
zim capture -o D:\migration\source --all

# On destination PC: restore
zim restore -i D:\migration\source --user-map OldUser:NewUser
```

See `docs\CLI-Reference.txt` for the full command reference.

## WiFi Transfer (no USB needed)

1. Run `zim-agent --role source --key mykey --dir C:\CapturedData` on the source PC
2. Run `zim-agent --role destination --key mykey --dir D:\RestoreData --peer <source-ip>` on the destination PC
3. Files transfer directly over the local network

## Full Disk Clone + WinPE Restore

For cases where per-app migration is insufficient:

1. Capture a full disk clone: `zim capture -o D:\migration --tier clone --volume C`
2. Build a bootable WinPE USB: `winpe\Build-WinPE.ps1` (requires Windows ADK)
3. Boot the destination PC from the WinPE USB and restore the image

See `docs\PXE-Boot-Guide.md` for network-based restore without USB.

## Persistent Backup Agent

Install on customer PCs for automatic scheduled backups to the company NAS:

1. Create a config file: `zim-backup status --config backup-config.json`
2. Edit the config with NAS connection, backup paths, and schedule
3. Run in tray mode: `zim-backup --config backup-config.json`
4. Or install as a service: `zim-backup install --config C:\path\to\backup-config.json`
5. Customer sees tray icon with backup status, can trigger manual backups or request restores

## Requirements

- **Source/Destination PCs:** Windows 10 or Windows 11 (x64)
- **No installation needed** — runs directly from USB
- **No internet required** for USB/network transfers
- Internet required on destination for package-based app reinstall (Tier 1)

## Support

- GitHub: https://github.com/domin0z/ZeroInstall
- License: GNU GPL v3
