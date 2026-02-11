namespace ZeroInstall.Core.Enums;

/// <summary>
/// BitLocker protection status for a volume.
/// </summary>
public enum BitLockerProtectionStatus
{
    /// <summary>
    /// Status could not be determined (manage-bde not available, or error).
    /// </summary>
    Unknown,

    /// <summary>
    /// Volume is not protected by BitLocker.
    /// </summary>
    NotProtected,

    /// <summary>
    /// Volume is BitLocker-encrypted but currently unlocked (keys in memory).
    /// Data can be read as plaintext, but suspending protection is recommended before cloning.
    /// </summary>
    Unlocked,

    /// <summary>
    /// Volume is BitLocker-encrypted and locked. Data reads return ciphertext.
    /// Must be unlocked before any migration or cloning operation.
    /// </summary>
    Locked,

    /// <summary>
    /// BitLocker protection is suspended (encryption key stored in clear on disk).
    /// Volume data can be read as plaintext. Safe to clone.
    /// </summary>
    Suspended
}
