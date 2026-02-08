using System.Text.Json;
using System.Text.Json.Serialization;
using ZeroInstall.Core.Enums;

namespace ZeroInstall.Core.Migration;

/// <summary>
/// Metadata about a captured disk image, written alongside the image file.
/// </summary>
public class DiskImageMetadata
{
    public string ImageId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CapturedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Hostname of the source machine.
    /// </summary>
    public string SourceHostname { get; set; } = string.Empty;

    /// <summary>
    /// OS version of the source machine (e.g., "Windows 10 Pro 22H2").
    /// </summary>
    public string SourceOsVersion { get; set; } = string.Empty;

    /// <summary>
    /// The volume/drive letter that was captured (e.g., "C:").
    /// </summary>
    public string SourceVolume { get; set; } = string.Empty;

    /// <summary>
    /// Total size of the source volume in bytes.
    /// </summary>
    public long SourceVolumeSizeBytes { get; set; }

    /// <summary>
    /// Used space on the source volume in bytes.
    /// </summary>
    public long SourceVolumeUsedBytes { get; set; }

    /// <summary>
    /// Size of the image file(s) in bytes.
    /// </summary>
    public long ImageSizeBytes { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DiskImageFormat Format { get; set; }

    /// <summary>
    /// Whether the image was compressed.
    /// </summary>
    public bool IsCompressed { get; set; }

    /// <summary>
    /// SHA-256 checksum of the image file (or of the combined chunks).
    /// </summary>
    public string? Checksum { get; set; }

    /// <summary>
    /// Whether the image was split into chunks (for FAT32 compatibility).
    /// </summary>
    public bool IsSplit { get; set; }

    /// <summary>
    /// Number of chunk files if split.
    /// </summary>
    public int ChunkCount { get; set; } = 1;

    /// <summary>
    /// Size of each chunk in bytes (except possibly the last).
    /// </summary>
    public long ChunkSizeBytes { get; set; }

    /// <summary>
    /// Checksums for individual chunks (if split).
    /// </summary>
    public List<string> ChunkChecksums { get; set; } = [];

    /// <summary>
    /// Whether Volume Shadow Copy was used for live capture.
    /// </summary>
    public bool UsedVss { get; set; }

    /// <summary>
    /// File system type of the source volume (e.g., "NTFS").
    /// </summary>
    public string FileSystemType { get; set; } = string.Empty;

    /// <summary>
    /// Gets the file extension for the image format.
    /// </summary>
    public static string GetExtension(DiskImageFormat format) => format switch
    {
        DiskImageFormat.Img => ".img",
        DiskImageFormat.Raw => ".raw",
        DiskImageFormat.Vhdx => ".vhdx",
        _ => ".img"
    };

    /// <summary>
    /// Gets the metadata file path for an image file.
    /// </summary>
    public static string GetMetadataPath(string imagePath)
    {
        return Path.ChangeExtension(imagePath, ".zim-meta.json");
    }

    /// <summary>
    /// Gets the chunk file path for a given chunk index.
    /// </summary>
    public static string GetChunkPath(string baseImagePath, int chunkIndex)
    {
        var dir = Path.GetDirectoryName(baseImagePath) ?? ".";
        var name = Path.GetFileNameWithoutExtension(baseImagePath);
        var ext = Path.GetExtension(baseImagePath);
        return Path.Combine(dir, $"{name}.part{chunkIndex:D4}{ext}");
    }

    /// <summary>
    /// Saves this metadata to a JSON file alongside the image.
    /// </summary>
    public async Task SaveAsync(string imagePath, CancellationToken ct = default)
    {
        var metaPath = GetMetadataPath(imagePath);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(metaPath, json, ct);
    }

    /// <summary>
    /// Loads metadata from the JSON file alongside an image.
    /// </summary>
    public static async Task<DiskImageMetadata?> LoadAsync(string imagePath, CancellationToken ct = default)
    {
        var metaPath = GetMetadataPath(imagePath);
        if (!File.Exists(metaPath)) return null;

        var json = await File.ReadAllTextAsync(metaPath, ct);
        return JsonSerializer.Deserialize<DiskImageMetadata>(json);
    }
}
