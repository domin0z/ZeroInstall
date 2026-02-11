using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Services;

/// <summary>
/// Firmware diagnostics and BCD management service using PowerShell WMI + bcdedit.
/// </summary>
internal class FirmwareService : IFirmwareService
{
    private readonly IProcessRunner _processRunner;
    private readonly ILogger<FirmwareService> _logger;

    public FirmwareService(IProcessRunner processRunner, ILogger<FirmwareService> logger)
    {
        _processRunner = processRunner;
        _logger = logger;
    }

    public async Task<FirmwareInfo> GetFirmwareInfoAsync(CancellationToken ct = default)
    {
        var info = new FirmwareInfo();

        try
        {
            // Run PowerShell to gather WMI data
            var psCommand = "$bios = Get-CimInstance Win32_BIOS | Select Manufacturer, SMBIOSBIOSVersion, ReleaseDate; " +
                "$cs = Get-CimInstance Win32_ComputerSystem | Select Manufacturer, Model; " +
                "$fw = (Get-ItemProperty 'HKLM:\\System\\CurrentControlSet\\Control' -Name PEFirmwareType -EA SilentlyContinue).PEFirmwareType; " +
                "$sb = try { [string](Confirm-SecureBootUEFI) } catch { 'NotSupported' }; " +
                "$tpm = Get-CimInstance -Namespace 'root\\cimv2\\Security\\MicrosoftTpm' -ClassName Win32_Tpm -EA SilentlyContinue | Select IsActivated_InitialValue, SpecVersion; " +
                "@{Bios=$bios; System=$cs; FirmwareType=$fw; SecureBoot=$sb; Tpm=$tpm} | ConvertTo-Json -Depth 3";

            var result = await _processRunner.RunAsync(
                "powershell", $"-NoProfile -Command \"{psCommand}\"", ct);

            if (result.Success && !string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                ParseWmiOutput(result.StandardOutput, info);
            }
            else
            {
                _logger.LogWarning("PowerShell firmware query failed: {Error}", result.StandardError);
            }

            // Get boot entries via bcdedit
            var bcdResult = await _processRunner.RunAsync("bcdedit", "/enum all", ct);
            if (bcdResult.Success && !string.IsNullOrWhiteSpace(bcdResult.StandardOutput))
            {
                info.BootEntries = ParseBcdEnum(bcdResult.StandardOutput);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to gather firmware information");
        }

        return info;
    }

    public async Task<bool> ExportBcdAsync(string exportPath, CancellationToken ct = default)
    {
        try
        {
            var result = await _processRunner.RunAsync("bcdedit", $"/export \"{exportPath}\"", ct);

            if (result.Success)
            {
                _logger.LogInformation("BCD store exported to {Path}", exportPath);
                return true;
            }

            _logger.LogWarning("BCD export failed: {Error}", result.StandardError);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export BCD store to {Path}", exportPath);
            return false;
        }
    }

    public async Task<bool> ImportBcdAsync(string importPath, CancellationToken ct = default)
    {
        try
        {
            var result = await _processRunner.RunAsync("bcdedit", $"/import \"{importPath}\"", ct);

            if (result.Success)
            {
                _logger.LogInformation("BCD store imported from {Path}", importPath);
                return true;
            }

            _logger.LogWarning("BCD import failed: {Error}", result.StandardError);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import BCD store from {Path}", importPath);
            return false;
        }
    }

    public async Task<IReadOnlyList<BcdBootEntry>> GetBootEntriesAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await _processRunner.RunAsync("bcdedit", "/enum all", ct);

            if (result.Success && !string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                return ParseBcdEnum(result.StandardOutput);
            }

            _logger.LogWarning("bcdedit /enum failed: {Error}", result.StandardError);
            return [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enumerate boot entries");
            return [];
        }
    }

    private void ParseWmiOutput(string json, FirmwareInfo info)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Firmware type
            if (root.TryGetProperty("FirmwareType", out var fwType))
            {
                info.FirmwareType = ParseFirmwareType(
                    fwType.ValueKind == JsonValueKind.Number ? fwType.GetInt32() : null);
            }

            // Secure Boot
            if (root.TryGetProperty("SecureBoot", out var sb))
            {
                info.SecureBoot = ParseSecureBootStatus(
                    sb.ValueKind == JsonValueKind.String ? sb.GetString() : null);
            }

            // BIOS info
            if (root.TryGetProperty("Bios", out var bios))
            {
                var (vendor, version, releaseDate) = ParseBiosInfo(bios);
                info.BiosVendor = vendor;
                info.BiosVersion = version;
                info.BiosReleaseDate = releaseDate;
            }

            // System info
            if (root.TryGetProperty("System", out var sys))
            {
                var (manufacturer, model) = ParseSystemInfo(sys);
                info.SystemManufacturer = manufacturer;
                info.SystemModel = model;
            }

            // TPM info
            if (root.TryGetProperty("Tpm", out var tpm))
            {
                var (present, version) = ParseTpmInfo(tpm);
                info.TpmPresent = present;
                info.TpmVersion = version;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse WMI JSON output");
        }
    }

    /// <summary>
    /// Parses the PEFirmwareType registry value into a <see cref="FirmwareType"/>.
    /// </summary>
    internal static FirmwareType ParseFirmwareType(int? peType)
    {
        return peType switch
        {
            1 => FirmwareType.Bios,
            2 => FirmwareType.Uefi,
            _ => FirmwareType.Unknown
        };
    }

    /// <summary>
    /// Parses the Confirm-SecureBootUEFI result string into a <see cref="SecureBootStatus"/>.
    /// </summary>
    internal static SecureBootStatus ParseSecureBootStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return SecureBootStatus.Unknown;

        return value.Trim() switch
        {
            "True" => SecureBootStatus.Enabled,
            "False" => SecureBootStatus.Disabled,
            "NotSupported" => SecureBootStatus.NotSupported,
            _ => SecureBootStatus.Unknown
        };
    }

    /// <summary>
    /// Parses BIOS information from a JSON element.
    /// </summary>
    internal static (string Vendor, string Version, string ReleaseDate) ParseBiosInfo(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
            return (string.Empty, string.Empty, string.Empty);

        var vendor = element.TryGetProperty("Manufacturer", out var mfr) && mfr.ValueKind == JsonValueKind.String
            ? mfr.GetString() ?? string.Empty
            : string.Empty;

        var version = element.TryGetProperty("SMBIOSBIOSVersion", out var ver) && ver.ValueKind == JsonValueKind.String
            ? ver.GetString() ?? string.Empty
            : string.Empty;

        var releaseDate = string.Empty;
        if (element.TryGetProperty("ReleaseDate", out var rd))
        {
            if (rd.ValueKind == JsonValueKind.String)
            {
                releaseDate = rd.GetString() ?? string.Empty;
            }
        }

        return (vendor, version, releaseDate);
    }

    /// <summary>
    /// Parses system information from a JSON element.
    /// </summary>
    internal static (string Manufacturer, string Model) ParseSystemInfo(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
            return (string.Empty, string.Empty);

        var manufacturer = element.TryGetProperty("Manufacturer", out var mfr) && mfr.ValueKind == JsonValueKind.String
            ? mfr.GetString() ?? string.Empty
            : string.Empty;

        var model = element.TryGetProperty("Model", out var mdl) && mdl.ValueKind == JsonValueKind.String
            ? mdl.GetString() ?? string.Empty
            : string.Empty;

        return (manufacturer, model);
    }

    /// <summary>
    /// Parses TPM information from a JSON element.
    /// </summary>
    internal static (bool Present, string Version) ParseTpmInfo(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
            return (false, string.Empty);

        var present = element.TryGetProperty("IsActivated_InitialValue", out var activated)
                      && activated.ValueKind == JsonValueKind.True;

        var version = string.Empty;
        if (element.TryGetProperty("SpecVersion", out var sv) && sv.ValueKind == JsonValueKind.String)
        {
            var raw = sv.GetString() ?? string.Empty;
            // SpecVersion is often "2.0, 0, 1.38" — take just the major version
            var commaIndex = raw.IndexOf(',');
            version = commaIndex > 0 ? raw[..commaIndex].Trim() : raw.Trim();
        }

        return (present, version);
    }

    /// <summary>
    /// Parses bcdedit /enum all output into a list of <see cref="BcdBootEntry"/>.
    /// </summary>
    internal static List<BcdBootEntry> ParseBcdEnum(string output)
    {
        var entries = new List<BcdBootEntry>();

        if (string.IsNullOrWhiteSpace(output))
            return entries;

        // Split into sections by blank lines
        var sections = SplitIntoSections(output);

        foreach (var section in sections)
        {
            if (section.Count == 0)
                continue;

            var entry = new BcdBootEntry();

            // First line is the entry type (e.g., "Windows Boot Manager", "Windows Boot Loader")
            entry.EntryType = section[0].Trim();

            // Skip separator line (dashes)
            var startIndex = 1;
            if (section.Count > 1 && section[1].Trim().StartsWith("---"))
                startIndex = 2;

            // Parse key-value pairs
            for (var i = startIndex; i < section.Count; i++)
            {
                var line = section[i];
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var (key, value) = ParseBcdKeyValue(line);
                if (string.IsNullOrEmpty(key))
                    continue;

                switch (key.ToLowerInvariant())
                {
                    case "identifier":
                        entry.Identifier = value;
                        if (value.Contains("{current}"))
                            entry.IsDefault = true;
                        break;
                    case "description":
                        entry.Description = value;
                        break;
                    case "device":
                        entry.Device = value;
                        break;
                    case "path":
                        entry.Path = value;
                        break;
                    default:
                        entry.Properties[key] = value;
                        break;
                }
            }

            // Only add entries that have an identifier
            if (!string.IsNullOrEmpty(entry.Identifier))
                entries.Add(entry);
        }

        return entries;
    }

    private static List<List<string>> SplitIntoSections(string output)
    {
        var sections = new List<List<string>>();
        var currentSection = new List<string>();

        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');

            if (string.IsNullOrWhiteSpace(line))
            {
                if (currentSection.Count > 0)
                {
                    sections.Add(currentSection);
                    currentSection = [];
                }
            }
            else
            {
                currentSection.Add(line);
            }
        }

        if (currentSection.Count > 0)
            sections.Add(currentSection);

        return sections;
    }

    private static (string Key, string Value) ParseBcdKeyValue(string line)
    {
        // BCD key-value lines use multiple spaces as separator
        // e.g., "identifier              {current}"
        // Find the first run of 2+ spaces after some non-space content
        var trimmed = line.TrimStart();
        var i = 0;

        // Skip past the key (non-space characters)
        while (i < trimmed.Length && trimmed[i] != ' ')
            i++;

        if (i >= trimmed.Length)
            return (trimmed, string.Empty);

        var key = trimmed[..i];

        // Skip past the spaces
        while (i < trimmed.Length && trimmed[i] == ' ')
            i++;

        if (i >= trimmed.Length)
            return (key, string.Empty);

        var value = trimmed[i..];
        return (key, value);
    }
}
