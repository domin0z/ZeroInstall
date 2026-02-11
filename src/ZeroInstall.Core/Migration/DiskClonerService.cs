using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;
using ZeroInstall.Core.Transport;

namespace ZeroInstall.Core.Migration;

/// <summary>
/// Tier 3 migrator: full volume cloning to .img/.raw/.vhdx image files.
/// Supports Volume Shadow Copy for live captures, compression, chunk splitting,
/// and integrity verification.
/// </summary>
public class DiskClonerService : IDiskCloner
{
    private const int BlockSize = 1024 * 1024; // 1 MB read blocks

    private readonly IProcessRunner _processRunner;
    private readonly ILogger<DiskClonerService> _logger;
    private readonly IBitLockerService? _bitLockerService;

    public DiskClonerService(
        IProcessRunner processRunner,
        ILogger<DiskClonerService> logger,
        IBitLockerService? bitLockerService = null)
    {
        _processRunner = processRunner;
        _logger = logger;
        _bitLockerService = bitLockerService;
    }

    /// <summary>
    /// Clones a volume to an image file in the specified format.
    /// </summary>
    public async Task CloneVolumeAsync(
        string volumePath,
        string outputImagePath,
        DiskImageFormat format,
        IProgress<TransferProgress>? progress = null,
        CancellationToken ct = default)
    {
        // BitLocker pre-check
        BitLockerStatus? bitLockerStatus = null;
        if (_bitLockerService is not null)
        {
            bitLockerStatus = await _bitLockerService.GetStatusAsync(volumePath, ct);

            if (bitLockerStatus.ProtectionStatus == BitLockerProtectionStatus.Locked)
            {
                throw new InvalidOperationException(
                    $"Volume {volumePath} is BitLocker-locked. Cloning a locked volume produces encrypted " +
                    "ciphertext that cannot be restored. Unlock the volume first using: " +
                    $"zim bitlocker unlock {volumePath} --recovery-password <key>");
            }

            if (bitLockerStatus.ProtectionStatus == BitLockerProtectionStatus.Unlocked)
            {
                _logger.LogWarning(
                    "Volume {Volume} is BitLocker-encrypted and unlocked. Data can be read, but " +
                    "suspending BitLocker protection before cloning is recommended for reliability. " +
                    "Use: manage-bde -protectors -disable {Volume}", volumePath, volumePath);
            }
        }

        var volumeInfo = GetVolumeInfo(volumePath);

        _logger.LogInformation("Starting volume clone: {Volume} ({Size} bytes) -> {Output} ({Format})",
            volumePath, volumeInfo.TotalSize, outputImagePath, format);

        var outputDir = Path.GetDirectoryName(outputImagePath);
        if (outputDir is not null && !Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        if (format == DiskImageFormat.Vhdx)
        {
            await CloneToVhdxAsync(volumePath, outputImagePath, volumeInfo, progress, ct);
        }
        else
        {
            await CloneToRawImageAsync(volumePath, outputImagePath, volumeInfo, progress, ct);
        }

        // Generate and save metadata
        var imageSizeBytes = File.Exists(outputImagePath) ? new FileInfo(outputImagePath).Length : 0;

        var metadata = new DiskImageMetadata
        {
            SourceHostname = Environment.MachineName,
            SourceOsVersion = Environment.OSVersion.VersionString,
            SourceVolume = volumePath,
            SourceVolumeSizeBytes = volumeInfo.TotalSize,
            SourceVolumeUsedBytes = volumeInfo.TotalSize - volumeInfo.FreeSpace,
            ImageSizeBytes = imageSizeBytes,
            Format = format,
            FileSystemType = volumeInfo.FileSystem,
            SourceWasBitLockerEncrypted = bitLockerStatus?.IsEncrypted ?? false,
            SourceBitLockerStatus = bitLockerStatus?.ProtectionStatus.ToString() ?? string.Empty,
            BitLockerWasSuspended = bitLockerStatus?.ProtectionStatus == BitLockerProtectionStatus.Suspended
        };

        // Compute checksum
        progress?.Report(new TransferProgress
        {
            CurrentItemName = "Computing image checksum...",
            CurrentItemIndex = 1,
            TotalItems = 1
        });

        if (File.Exists(outputImagePath))
        {
            metadata.Checksum = await ChecksumHelper.ComputeFileAsync(outputImagePath, ct);
        }

        await metadata.SaveAsync(outputImagePath, ct);

        _logger.LogInformation("Volume clone complete: {Output} ({ImageSize} bytes, checksum: {Checksum})",
            outputImagePath, imageSizeBytes, metadata.Checksum?[..16]);
    }

    /// <summary>
    /// Restores an image file to a target volume.
    /// </summary>
    public async Task RestoreImageAsync(
        string imagePath,
        string targetVolumePath,
        IProgress<TransferProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(imagePath))
            throw new FileNotFoundException("Image file not found", imagePath);

        var metadata = await DiskImageMetadata.LoadAsync(imagePath, ct);

        // Check if image is split — reassemble first
        string restoreSourcePath = imagePath;
        if (metadata is { IsSplit: true, ChunkCount: > 1 })
        {
            _logger.LogInformation("Reassembling {Count} chunks before restore", metadata.ChunkCount);
            var reassembledPath = Path.Combine(Path.GetTempPath(), $"zim-reassemble-{Guid.NewGuid():N}.img");

            await ImageSplitter.ReassembleAsync(reassembledPath, metadata.ChunkCount, imagePath, progress, ct);
            restoreSourcePath = reassembledPath;
        }

        if (metadata?.Format == DiskImageFormat.Vhdx)
        {
            await RestoreFromVhdxAsync(restoreSourcePath, targetVolumePath, progress, ct);
        }
        else
        {
            await RestoreFromRawImageAsync(restoreSourcePath, targetVolumePath, progress, ct);
        }

        // Clean up temporary reassembled file
        if (restoreSourcePath != imagePath && File.Exists(restoreSourcePath))
            File.Delete(restoreSourcePath);

        _logger.LogInformation("Image restored to {Volume}", targetVolumePath);
    }

    /// <summary>
    /// Verifies the integrity of an image file by comparing its checksum to the stored metadata.
    /// </summary>
    public async Task<bool> VerifyImageAsync(string imagePath, CancellationToken ct = default)
    {
        var metadata = await DiskImageMetadata.LoadAsync(imagePath, ct);
        if (metadata is null)
        {
            _logger.LogWarning("No metadata found for {Image}, cannot verify", imagePath);
            return false;
        }

        if (string.IsNullOrEmpty(metadata.Checksum))
        {
            _logger.LogWarning("No checksum in metadata for {Image}", imagePath);
            return false;
        }

        // For split images, verify each chunk
        if (metadata.IsSplit && metadata.ChunkChecksums.Count > 0)
        {
            return await VerifyChunksAsync(imagePath, metadata, ct);
        }

        // For single file images
        if (!File.Exists(imagePath))
        {
            _logger.LogWarning("Image file not found: {Image}", imagePath);
            return false;
        }

        var actualChecksum = await ChecksumHelper.ComputeFileAsync(imagePath, ct);
        var match = string.Equals(actualChecksum, metadata.Checksum, StringComparison.OrdinalIgnoreCase);

        if (match)
            _logger.LogInformation("Image verification passed: {Image}", imagePath);
        else
            _logger.LogWarning("Image verification FAILED: {Image} (expected {Expected}, got {Actual})",
                imagePath, metadata.Checksum[..16], actualChecksum[..16]);

        return match;
    }

    /// <summary>
    /// Captures data from migration items (IMigrator implementation).
    /// For Tier 3, this clones the system volume.
    /// </summary>
    public async Task CaptureAsync(
        IReadOnlyList<MigrationItem> items,
        string outputPath,
        IProgress<TransferProgress>? progress = null,
        CancellationToken ct = default)
    {
        // For disk cloning, we clone the system volume
        var systemDrive = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? "C:\\";
        var volumeLetter = systemDrive.TrimEnd('\\');
        var imagePath = Path.Combine(outputPath, $"{Environment.MachineName}-{volumeLetter.Replace(":", "")}.img");

        foreach (var item in items.Where(i => i.IsSelected && i.EffectiveTier == MigrationTier.FullClone))
            item.Status = MigrationItemStatus.InProgress;

        await CloneVolumeAsync(volumeLetter, imagePath, DiskImageFormat.Img, progress, ct);

        foreach (var item in items.Where(i => i.IsSelected && i.EffectiveTier == MigrationTier.FullClone))
            item.Status = MigrationItemStatus.Completed;
    }

    /// <summary>
    /// Restores a previously captured disk image (IMigrator implementation).
    /// </summary>
    public async Task RestoreAsync(
        string inputPath,
        IReadOnlyList<UserMapping> userMappings,
        IProgress<TransferProgress>? progress = null,
        CancellationToken ct = default)
    {
        // Find the image file in the input path
        var imageFiles = Directory.GetFiles(inputPath, "*.img")
            .Concat(Directory.GetFiles(inputPath, "*.raw"))
            .Concat(Directory.GetFiles(inputPath, "*.vhdx"))
            .ToArray();

        if (imageFiles.Length == 0)
            throw new FileNotFoundException("No disk image files found in " + inputPath);

        var imagePath = imageFiles[0];
        var metadata = await DiskImageMetadata.LoadAsync(imagePath, ct);
        var targetVolume = metadata?.SourceVolume ?? "C:";

        await RestoreImageAsync(imagePath, targetVolume, progress, ct);
    }

    /// <summary>
    /// Creates a Volume Shadow Copy and returns the shadow copy device path.
    /// </summary>
    internal async Task<VssShadowCopy?> CreateVssShadowAsync(string volumePath, CancellationToken ct)
    {
        try
        {
            // Use vssadmin to create a shadow copy
            var volume = volumePath.EndsWith("\\") ? volumePath : volumePath + "\\";
            var result = await _processRunner.RunAsync(
                "vssadmin", $"create shadow /for={volume}", ct);

            if (!result.Success)
            {
                _logger.LogWarning("VSS shadow creation failed: {Error}", result.StandardError);
                return null;
            }

            // Parse the shadow copy device path from output
            // Expected: "Shadow Copy Device Name: \\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy1"
            var devicePath = ParseVssDevicePath(result.StandardOutput);
            if (devicePath is null)
            {
                _logger.LogWarning("Failed to parse VSS device path from output");
                return null;
            }

            var shadowId = ParseVssShadowId(result.StandardOutput);

            _logger.LogInformation("Created VSS shadow copy: {DevicePath}", devicePath);
            return new VssShadowCopy { DevicePath = devicePath, ShadowId = shadowId ?? string.Empty };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "VSS shadow creation failed");
            return null;
        }
    }

    /// <summary>
    /// Deletes a Volume Shadow Copy.
    /// </summary>
    internal async Task DeleteVssShadowAsync(string shadowId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(shadowId)) return;

        try
        {
            await _processRunner.RunAsync(
                "vssadmin", $"delete shadows /shadow={shadowId} /quiet", ct);
            _logger.LogDebug("Deleted VSS shadow copy: {Id}", shadowId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete VSS shadow copy {Id}", shadowId);
        }
    }

    private async Task CloneToRawImageAsync(
        string volumePath,
        string outputPath,
        VolumeInfo volumeInfo,
        IProgress<TransferProgress>? progress,
        CancellationToken ct)
    {
        // Try to create a VSS shadow for live capture
        var shadow = await CreateVssShadowAsync(volumePath, ct);
        var sourcePath = shadow?.DevicePath ?? $@"\\.\{volumePath}";

        try
        {
            // Use a process-based approach with raw device reading
            // On Windows, we use PowerShell to read the volume block by block
            var script = BuildRawCopyScript(sourcePath, outputPath, volumeInfo.TotalSize);

            _logger.LogDebug("Starting raw image copy from {Source}", sourcePath);

            var result = await _processRunner.RunAsync(
                "powershell",
                $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{script}\"",
                ct);

            if (!result.Success)
            {
                _logger.LogWarning("PowerShell raw copy failed, falling back to block copy: {Error}",
                    result.StandardError);

                // Fallback: use direct FileStream copy (requires admin)
                await BlockCopyAsync(sourcePath, outputPath, volumeInfo.TotalSize, progress, ct);
            }
        }
        finally
        {
            if (shadow is not null)
                await DeleteVssShadowAsync(shadow.ShadowId, ct);
        }
    }

    private async Task CloneToVhdxAsync(
        string volumePath,
        string outputPath,
        VolumeInfo volumeInfo,
        IProgress<TransferProgress>? progress,
        CancellationToken ct)
    {
        // Use PowerShell with Hyper-V module to create VHDX
        var sizeBytes = volumeInfo.TotalSize;
        var script = $"New-VHD -Path '{outputPath.Replace("'", "''")}' -SizeBytes {sizeBytes} -Dynamic";

        _logger.LogDebug("Creating VHDX: {Script}", script);

        var createResult = await _processRunner.RunAsync(
            "powershell",
            $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{script}\"",
            ct);

        if (!createResult.Success)
        {
            _logger.LogWarning("VHDX creation via Hyper-V failed: {Error}. " +
                "Falling back to raw image with .vhdx extension.", createResult.StandardError);

            // Fallback: create raw image (at least preserves the data)
            await CloneToRawImageAsync(volumePath, outputPath, volumeInfo, progress, ct);
            return;
        }

        // Mount and copy using disk2vhd-style approach
        var copyScript = $@"
            $vhd = Mount-VHD -Path '{outputPath.Replace("'", "''")}' -Passthru
            $disk = $vhd | Get-Disk
            Initialize-Disk -Number $disk.Number -PartitionStyle GPT -ErrorAction SilentlyContinue
            $part = New-Partition -DiskNumber $disk.Number -UseMaximumSize -AssignDriveLetter
            Format-Volume -Partition $part -FileSystem NTFS -Confirm:$false
            $destDrive = $part.DriveLetter + ':\'
            robocopy '{volumePath}\' $destDrive /E /COPYALL /R:1 /W:1 /NFL /NDL /NP
            Dismount-VHD -Path '{outputPath.Replace("'", "''")}'
        ";

        await _processRunner.RunAsync(
            "powershell",
            $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{copyScript}\"",
            ct);

        _logger.LogInformation("VHDX clone complete: {Path}", outputPath);
    }

    private async Task RestoreFromRawImageAsync(
        string imagePath,
        string targetVolumePath,
        IProgress<TransferProgress>? progress,
        CancellationToken ct)
    {
        var imageSize = new FileInfo(imagePath).Length;
        var targetPath = $@"\\.\{targetVolumePath}";

        _logger.LogInformation("Restoring raw image to {Volume} ({Size} bytes)", targetVolumePath, imageSize);

        await BlockCopyAsync(imagePath, targetPath, imageSize, progress, ct);
    }

    private async Task RestoreFromVhdxAsync(
        string imagePath,
        string targetVolumePath,
        IProgress<TransferProgress>? progress,
        CancellationToken ct)
    {
        // Mount VHDX and copy files
        var script = $@"
            $vhd = Mount-VHD -Path '{imagePath.Replace("'", "''")}' -ReadOnly -Passthru
            $disk = $vhd | Get-Disk
            $parts = Get-Partition -DiskNumber $disk.Number | Where-Object {{ $_.Type -ne 'Reserved' -and $_.Size -gt 1GB }}
            $srcDrive = ($parts | Select-Object -First 1).DriveLetter + ':\'
            robocopy $srcDrive '{targetVolumePath}\' /E /COPYALL /R:1 /W:1 /NFL /NDL /NP
            Dismount-VHD -Path '{imagePath.Replace("'", "''")}'
        ";

        await _processRunner.RunAsync(
            "powershell",
            $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{script}\"",
            ct);

        _logger.LogInformation("VHDX restore complete to {Volume}", targetVolumePath);
    }

    /// <summary>
    /// Block-by-block copy between source and destination paths.
    /// </summary>
    internal static async Task BlockCopyAsync(
        string sourcePath,
        string destPath,
        long totalBytes,
        IProgress<TransferProgress>? progress,
        CancellationToken ct)
    {
        var buffer = new byte[BlockSize];
        long bytesCopied = 0;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        await using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite, BlockSize, useAsync: true);
        await using var dest = new FileStream(destPath, FileMode.Create, FileAccess.Write,
            FileShare.None, BlockSize, useAsync: true);

        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer, ct)) > 0)
        {
            await dest.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            bytesCopied += bytesRead;

            var elapsed = stopwatch.ElapsedMilliseconds;
            var bytesPerSecond = elapsed > 0 ? bytesCopied * 1000 / elapsed : 0;
            var remaining = bytesPerSecond > 0 && totalBytes > bytesCopied
                ? TimeSpan.FromSeconds((double)(totalBytes - bytesCopied) / bytesPerSecond)
                : (TimeSpan?)null;

            progress?.Report(new TransferProgress
            {
                CurrentItemName = "Cloning volume...",
                CurrentItemIndex = 1,
                TotalItems = 1,
                CurrentItemBytesTransferred = bytesCopied,
                CurrentItemTotalBytes = totalBytes,
                OverallBytesTransferred = bytesCopied,
                OverallTotalBytes = totalBytes,
                BytesPerSecond = bytesPerSecond,
                EstimatedTimeRemaining = remaining
            });
        }
    }

    internal static string BuildRawCopyScript(string sourcePath, string outputPath, long totalSize)
    {
        // PowerShell script to read raw device blocks and write to file
        return $@"
            $source = [System.IO.File]::Open('{sourcePath.Replace("'", "''")}', 'Open', 'Read', 'ReadWrite')
            $dest = [System.IO.File]::Open('{outputPath.Replace("'", "''")}', 'Create', 'Write', 'None')
            $buffer = New-Object byte[] 1048576
            $total = 0
            while (($read = $source.Read($buffer, 0, $buffer.Length)) -gt 0) {{
                $dest.Write($buffer, 0, $read)
                $total += $read
            }}
            $source.Close()
            $dest.Close()
            Write-Output ""Copied $total bytes""
        ";
    }

    private async Task<bool> VerifyChunksAsync(
        string baseImagePath,
        DiskImageMetadata metadata,
        CancellationToken ct)
    {
        for (int i = 0; i < metadata.ChunkChecksums.Count; i++)
        {
            var chunkPath = DiskImageMetadata.GetChunkPath(baseImagePath, i);
            if (!File.Exists(chunkPath))
            {
                _logger.LogWarning("Chunk file not found: {Path}", chunkPath);
                return false;
            }

            var actualChecksum = await ChecksumHelper.ComputeFileAsync(chunkPath, ct);
            if (!string.Equals(actualChecksum, metadata.ChunkChecksums[i], StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Chunk {Index} verification FAILED: expected {Expected}, got {Actual}",
                    i, metadata.ChunkChecksums[i][..16], actualChecksum[..16]);
                return false;
            }
        }

        _logger.LogInformation("All {Count} chunks verified successfully", metadata.ChunkChecksums.Count);
        return true;
    }

    internal static string? ParseVssDevicePath(string vssOutput)
    {
        // Look for "Shadow Copy Device Name: \\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy1"
        foreach (var line in vssOutput.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("Shadow Copy Device Name:", StringComparison.OrdinalIgnoreCase))
            {
                var path = trimmed["Shadow Copy Device Name:".Length..].Trim();
                return string.IsNullOrEmpty(path) ? null : path;
            }
        }
        return null;
    }

    internal static string? ParseVssShadowId(string vssOutput)
    {
        // Look for "Shadow Copy ID: {GUID}"
        foreach (var line in vssOutput.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("Shadow Copy ID:", StringComparison.OrdinalIgnoreCase))
            {
                var id = trimmed["Shadow Copy ID:".Length..].Trim();
                return string.IsNullOrEmpty(id) ? null : id;
            }
        }
        return null;
    }

    internal static VolumeInfo GetVolumeInfo(string volumePath)
    {
        try
        {
            var root = volumePath.EndsWith("\\") ? volumePath : volumePath + "\\";
            var drive = new DriveInfo(root);
            return new VolumeInfo
            {
                TotalSize = drive.TotalSize,
                FreeSpace = drive.AvailableFreeSpace,
                FileSystem = drive.DriveFormat
            };
        }
        catch
        {
            return new VolumeInfo
            {
                TotalSize = 0,
                FreeSpace = 0,
                FileSystem = "Unknown"
            };
        }
    }
}

/// <summary>
/// Information about a volume being cloned.
/// </summary>
public class VolumeInfo
{
    public long TotalSize { get; set; }
    public long FreeSpace { get; set; }
    public string FileSystem { get; set; } = string.Empty;
}

/// <summary>
/// Represents a VSS shadow copy created for live volume capture.
/// </summary>
internal class VssShadowCopy
{
    public string DevicePath { get; set; } = string.Empty;
    public string ShadowId { get; set; } = string.Empty;
}
