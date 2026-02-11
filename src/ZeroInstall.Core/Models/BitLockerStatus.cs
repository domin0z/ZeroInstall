using ZeroInstall.Core.Enums;

namespace ZeroInstall.Core.Models;

/// <summary>
/// Parsed BitLocker status for a volume, obtained from manage-bde.exe output.
/// </summary>
public class BitLockerStatus
{
    /// <summary>
    /// Volume path (e.g., "C:").
    /// </summary>
    public string VolumePath { get; set; } = string.Empty;

    /// <summary>
    /// Overall protection status.
    /// </summary>
    public BitLockerProtectionStatus ProtectionStatus { get; set; } = BitLockerProtectionStatus.Unknown;

    /// <summary>
    /// Whether the volume has BitLocker encryption (protection on, or suspended with encryption present).
    /// </summary>
    public bool IsEncrypted => ProtectionStatus is BitLockerProtectionStatus.Unlocked
        or BitLockerProtectionStatus.Locked
        or BitLockerProtectionStatus.Suspended;

    /// <summary>
    /// Lock status string from manage-bde (e.g., "Locked", "Unlocked").
    /// </summary>
    public string LockStatus { get; set; } = string.Empty;

    /// <summary>
    /// Encryption method (e.g., "XTS-AES 128", "AES-CBC 256").
    /// </summary>
    public string EncryptionMethod { get; set; } = string.Empty;

    /// <summary>
    /// Conversion status (e.g., "Fully Encrypted", "Fully Decrypted", "Encryption in Progress").
    /// </summary>
    public string ConversionStatus { get; set; } = string.Empty;

    /// <summary>
    /// Percentage of the volume that is encrypted (0.0 to 100.0).
    /// </summary>
    public double PercentageEncrypted { get; set; }

    /// <summary>
    /// List of key protector types (e.g., "TPM", "Recovery Password", "Numerical Password").
    /// </summary>
    public List<string> KeyProtectors { get; set; } = [];

    /// <summary>
    /// Raw output from manage-bde, preserved for diagnostics.
    /// </summary>
    public string RawOutput { get; set; } = string.Empty;
}
