using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Services;

/// <summary>
/// Service for querying firmware information and managing BCD boot configuration.
/// </summary>
public interface IFirmwareService
{
    /// <summary>
    /// Gets comprehensive firmware information including BIOS, Secure Boot, TPM, and boot entries.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Populated <see cref="FirmwareInfo"/> with all available data.</returns>
    Task<FirmwareInfo> GetFirmwareInfoAsync(CancellationToken ct = default);

    /// <summary>
    /// Exports the BCD store to a file for backup.
    /// </summary>
    /// <param name="exportPath">Destination file path for the BCD backup.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if export succeeded.</returns>
    Task<bool> ExportBcdAsync(string exportPath, CancellationToken ct = default);

    /// <summary>
    /// Imports a BCD store from a backup file.
    /// WARNING: This overwrites the current BCD store. Use with caution.
    /// </summary>
    /// <param name="importPath">Path to the BCD backup file.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if import succeeded.</returns>
    Task<bool> ImportBcdAsync(string importPath, CancellationToken ct = default);

    /// <summary>
    /// Gets the list of boot entries from the BCD store.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of parsed BCD boot entries.</returns>
    Task<IReadOnlyList<BcdBootEntry>> GetBootEntriesAsync(CancellationToken ct = default);
}
