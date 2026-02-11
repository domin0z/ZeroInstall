using System.Globalization;
using Microsoft.Extensions.Logging;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Services;

/// <summary>
/// BitLocker management service using manage-bde.exe.
/// manage-bde is available on all BitLocker-capable Windows editions (Pro/Enterprise/Education).
/// </summary>
internal class BitLockerService : IBitLockerService
{
    private readonly IProcessRunner _processRunner;
    private readonly ILogger<BitLockerService> _logger;

    public BitLockerService(IProcessRunner processRunner, ILogger<BitLockerService> logger)
    {
        _processRunner = processRunner;
        _logger = logger;
    }

    public async Task<BitLockerStatus> GetStatusAsync(string volumePath, CancellationToken ct = default)
    {
        var normalizedPath = NormalizeVolumePath(volumePath);

        try
        {
            var result = await _processRunner.RunAsync(
                "manage-bde", $"-status {normalizedPath}", ct);

            if (!result.Success)
            {
                _logger.LogWarning("manage-bde -status failed for {Volume}: {Error}",
                    normalizedPath, result.StandardError);
                return new BitLockerStatus
                {
                    VolumePath = normalizedPath,
                    ProtectionStatus = BitLockerProtectionStatus.Unknown,
                    RawOutput = result.StandardOutput + result.StandardError
                };
            }

            return ParseBitLockerStatus(normalizedPath, result.StandardOutput);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query BitLocker status for {Volume}", normalizedPath);
            return new BitLockerStatus
            {
                VolumePath = normalizedPath,
                ProtectionStatus = BitLockerProtectionStatus.Unknown
            };
        }
    }

    public async Task<bool> UnlockVolumeAsync(string volumePath, string? recoveryPassword = null,
        string? recoveryKeyPath = null, CancellationToken ct = default)
    {
        var normalizedPath = NormalizeVolumePath(volumePath);

        string arguments;
        if (!string.IsNullOrEmpty(recoveryPassword))
        {
            arguments = $"-unlock {normalizedPath} -RecoveryPassword {recoveryPassword}";
        }
        else if (!string.IsNullOrEmpty(recoveryKeyPath))
        {
            arguments = $"-unlock {normalizedPath} -RecoveryKey \"{recoveryKeyPath}\"";
        }
        else
        {
            _logger.LogError("No recovery password or key file provided for BitLocker unlock");
            return false;
        }

        try
        {
            var result = await _processRunner.RunAsync("manage-bde", arguments, ct);

            if (result.Success)
            {
                _logger.LogInformation("BitLocker volume {Volume} unlocked successfully", normalizedPath);
                return true;
            }

            _logger.LogWarning("BitLocker unlock failed for {Volume}: {Error}",
                normalizedPath, result.StandardError);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unlock BitLocker volume {Volume}", normalizedPath);
            return false;
        }
    }

    public async Task<bool> SuspendProtectionAsync(string volumePath, int rebootCount = 0,
        CancellationToken ct = default)
    {
        var normalizedPath = NormalizeVolumePath(volumePath);

        var arguments = rebootCount > 0
            ? $"-protectors -disable {normalizedPath} -RebootCount {rebootCount}"
            : $"-protectors -disable {normalizedPath}";

        try
        {
            var result = await _processRunner.RunAsync("manage-bde", arguments, ct);

            if (result.Success)
            {
                _logger.LogInformation("BitLocker protection suspended on {Volume} (reboot count: {Count})",
                    normalizedPath, rebootCount == 0 ? "indefinite" : rebootCount);
                return true;
            }

            _logger.LogWarning("BitLocker suspend failed for {Volume}: {Error}",
                normalizedPath, result.StandardError);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to suspend BitLocker protection on {Volume}", normalizedPath);
            return false;
        }
    }

    public async Task<bool> ResumeProtectionAsync(string volumePath, CancellationToken ct = default)
    {
        var normalizedPath = NormalizeVolumePath(volumePath);

        try
        {
            var result = await _processRunner.RunAsync(
                "manage-bde", $"-protectors -enable {normalizedPath}", ct);

            if (result.Success)
            {
                _logger.LogInformation("BitLocker protection resumed on {Volume}", normalizedPath);
                return true;
            }

            _logger.LogWarning("BitLocker resume failed for {Volume}: {Error}",
                normalizedPath, result.StandardError);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resume BitLocker protection on {Volume}", normalizedPath);
            return false;
        }
    }

    /// <summary>
    /// Normalizes a volume path to "X:" format for manage-bde.
    /// </summary>
    internal static string NormalizeVolumePath(string volumePath)
    {
        if (string.IsNullOrWhiteSpace(volumePath))
            return string.Empty;

        // Strip trailing backslashes
        var path = volumePath.TrimEnd('\\', '/');

        // If it's just a letter, add colon
        if (path.Length == 1 && char.IsLetter(path[0]))
            return path.ToUpperInvariant() + ":";

        // If it's "X:", normalize case
        if (path.Length == 2 && char.IsLetter(path[0]) && path[1] == ':')
            return char.ToUpperInvariant(path[0]) + ":";

        return path;
    }

    /// <summary>
    /// Parses manage-bde -status output into a <see cref="BitLockerStatus"/> object.
    /// </summary>
    internal static BitLockerStatus ParseBitLockerStatus(string volumePath, string output)
    {
        var status = new BitLockerStatus
        {
            VolumePath = volumePath,
            RawOutput = output
        };

        if (string.IsNullOrWhiteSpace(output))
        {
            status.ProtectionStatus = BitLockerProtectionStatus.Unknown;
            return status;
        }

        var lines = output.Split('\n');
        var keyProtectorSection = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            if (TryParseField(line, "Conversion Status:", out var conversionStatus))
            {
                status.ConversionStatus = conversionStatus;
                keyProtectorSection = false;
            }
            else if (TryParseField(line, "Encryption Method:", out var encMethod))
            {
                status.EncryptionMethod = encMethod;
                keyProtectorSection = false;
            }
            else if (TryParseField(line, "Percentage Encrypted:", out var pctStr))
            {
                status.PercentageEncrypted = ParsePercentage(pctStr);
                keyProtectorSection = false;
            }
            else if (TryParseField(line, "Lock Status:", out var lockStatus))
            {
                status.LockStatus = lockStatus;
                keyProtectorSection = false;
            }
            else if (TryParseField(line, "Protection Status:", out var protStatus))
            {
                // Determine combined protection status
                // Will be refined after all fields are parsed
                keyProtectorSection = false;
            }
            else if (line.StartsWith("Key Protectors:", StringComparison.OrdinalIgnoreCase))
            {
                keyProtectorSection = true;
            }
            else if (keyProtectorSection && !string.IsNullOrWhiteSpace(line))
            {
                // Key protector lines look like "    TPM" or "    Numerical Password"
                status.KeyProtectors.Add(line.Trim());
            }
        }

        // Determine the combined protection status
        status.ProtectionStatus = DetermineProtectionStatus(output, status.LockStatus, status.ConversionStatus);

        return status;
    }

    private static BitLockerProtectionStatus DetermineProtectionStatus(
        string output, string lockStatus, string conversionStatus)
    {
        // Check if protection is off
        var protectionOff = output.Contains("Protection Status:") &&
                            ContainsFieldValue(output, "Protection Status:", "Protection Off");

        var protectionOn = output.Contains("Protection Status:") &&
                           ContainsFieldValue(output, "Protection Status:", "Protection On");

        var isFullyDecrypted = conversionStatus.Contains("Fully Decrypted", StringComparison.OrdinalIgnoreCase);
        var isLocked = lockStatus.Contains("Locked", StringComparison.OrdinalIgnoreCase) &&
                       !lockStatus.Contains("Unlocked", StringComparison.OrdinalIgnoreCase);

        if (protectionOn && isLocked)
            return BitLockerProtectionStatus.Locked;

        if (protectionOn && !isLocked)
            return BitLockerProtectionStatus.Unlocked;

        if (protectionOff && !isFullyDecrypted)
            return BitLockerProtectionStatus.Suspended;

        if (protectionOff && isFullyDecrypted)
            return BitLockerProtectionStatus.NotProtected;

        // If we found no protection status field at all
        if (!output.Contains("Protection Status:", StringComparison.OrdinalIgnoreCase))
            return BitLockerProtectionStatus.Unknown;

        return BitLockerProtectionStatus.NotProtected;
    }

    private static bool ContainsFieldValue(string output, string fieldName, string value)
    {
        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.Trim();
            if (TryParseField(line, fieldName, out var fieldValue) &&
                fieldValue.Contains(value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static bool TryParseField(string line, string fieldName, out string value)
    {
        value = string.Empty;
        if (!line.StartsWith(fieldName, StringComparison.OrdinalIgnoreCase))
            return false;

        value = line[fieldName.Length..].Trim();
        return true;
    }

    private static double ParsePercentage(string value)
    {
        // Handle "100.0%", "50.5%", "0%", etc.
        var cleaned = value.TrimEnd('%', ' ');
        return double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out var pct) ? pct : 0;
    }
}
