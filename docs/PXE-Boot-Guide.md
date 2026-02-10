# PXE Boot Guide for ZeroInstall WinPE Restore

This guide covers setting up PXE (network) boot so destination PCs can boot directly into the ZeroInstall WinPE restore environment without a USB drive.

## Overview

```
NAS / PXE Server                      Destination PC
┌─────────────────┐                   ┌──────────────────┐
│ DHCP (option 66) │  ← PXE request ─ │ Network boot     │
│ TFTP server      │  ── boot files → │ Loads WinPE      │
│ Image share (SMB)│  ← restore req ─ │ Runs zim-winpe   │
└─────────────────┘                   └──────────────────┘
```

**Flow:** Destination PC boots from network → DHCP points to TFTP server → TFTP serves WinPE boot files → WinPE starts → `zim-winpe` launches → user selects image from network share → restore proceeds.

## Prerequisites

- A server or NAS on the same network as the destination PC
- DHCP server with PXE options (or a separate proxy DHCP)
- TFTP server software
- The WinPE ISO or extracted WinPE files (built with `Build-WinPE.ps1`)
- Disk images stored on an accessible SMB share

## Server Setup

### Option A: Windows Server (WDS)

Windows Deployment Services provides built-in PXE/TFTP:

1. Install WDS role via Server Manager
2. Configure WDS and point to your WinPE boot image (`boot.wim`)
3. Add the WinPE boot image: **Boot Images → Add Boot Image → select `boot.wim`**
4. DHCP will automatically be configured with PXE options

### Option B: Linux / Synology NAS (dnsmasq + TFTP)

1. **Install dnsmasq** (provides DHCP proxy + TFTP):
   ```bash
   sudo apt install dnsmasq
   ```

2. **Configure `/etc/dnsmasq.conf`**:
   ```ini
   # Interface to listen on
   interface=eth0

   # Enable TFTP
   enable-tftp
   tftp-root=/srv/tftp

   # PXE boot options
   dhcp-boot=pxelinux.0
   # For UEFI clients:
   dhcp-match=set:efi-x86_64,option:client-arch,7
   dhcp-boot=tag:efi-x86_64,bootmgfw.efi

   # If you have a separate DHCP server, use proxy mode:
   dhcp-range=192.168.1.0,proxy
   ```

3. **Extract WinPE files to TFTP root**:
   ```bash
   # Mount the WinPE ISO
   sudo mount -o loop ZeroInstall-WinPE.iso /mnt/iso

   # Copy boot files
   sudo mkdir -p /srv/tftp
   sudo cp -r /mnt/iso/* /srv/tftp/

   # For UEFI boot, copy the EFI boot file
   sudo cp /mnt/iso/EFI/Boot/bootx64.efi /srv/tftp/bootmgfw.efi
   ```

4. **Restart dnsmasq**:
   ```bash
   sudo systemctl restart dnsmasq
   ```

### Option C: Dedicated PXE Server (Serva / Tiny PXE Server)

For Windows-based simple PXE setups:

1. Download [Serva](https://www.vercot.com/~serva/) or [Tiny PXE Server](https://erwan.labalec.fr/tinypxeserver/)
2. Point the TFTP root to the extracted WinPE files
3. Configure DHCP proxy mode if you already have a DHCP server

## DHCP Configuration

If using a standalone DHCP server (router, pfSense, etc.), set these DHCP options:

| Option | Value | Purpose |
|--------|-------|---------|
| 66 (TFTP Server) | IP of your PXE server | Where to get boot files |
| 67 (Boot filename) | `pxelinux.0` (BIOS) or `bootmgfw.efi` (UEFI) | Boot loader file |

**For UEFI-only networks**, use option 67 = `bootmgfw.efi`.

## Image Storage

Store disk images on an SMB share accessible from the WinPE environment:

```
\\NAS\ZeroInstall\Images\
├── DESKTOP-ABC.img
├── DESKTOP-ABC.zim-meta.json
├── LAPTOP-XYZ.vhdx
└── LAPTOP-XYZ.zim-meta.json
```

In WinPE, map the network share before running zim-winpe:
```cmd
net use Z: \\NAS\ZeroInstall\Images /user:username password
X:\ZeroInstall\zim-winpe.exe
```

To automate this, modify the `startnet.cmd` in the WinPE image (done during `Build-WinPE.ps1`).

## Client Setup

1. Enter the destination PC's BIOS/UEFI settings
2. Enable **Network Boot** / **PXE Boot**
3. Set boot order: Network first (or use one-time boot menu, usually F12)
4. Boot the PC — it will:
   - Request IP via DHCP
   - Receive PXE server address
   - Download boot files via TFTP
   - Launch WinPE
   - Auto-start `zim-winpe`

## Troubleshooting

| Problem | Solution |
|---------|----------|
| PC doesn't attempt PXE boot | Enable network boot in BIOS; check boot order |
| DHCP timeout | Verify DHCP options 66/67; check firewall (TFTP uses UDP 69) |
| TFTP transfer fails | Check TFTP server logs; ensure boot files are in TFTP root |
| WinPE boots but no network | Inject NIC drivers into WinPE image using `Add-Drivers.ps1` |
| Can't access SMB share | Check credentials; ensure SMB is enabled on the NAS; try IP address instead of hostname |
| Slow TFTP transfer | Normal for large WinPE images over TFTP; consider HTTP boot if supported |

## Security Considerations

- PXE boot is inherently insecure on untrusted networks — anyone on the LAN can boot from your server
- Use on isolated/trusted technician networks only
- Consider VLAN isolation for the PXE segment
- SMB shares should use dedicated service accounts with minimal permissions
