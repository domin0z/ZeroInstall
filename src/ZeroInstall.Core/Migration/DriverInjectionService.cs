using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ZeroInstall.Core.Discovery;

namespace ZeroInstall.Core.Migration;

/// <summary>
/// Result of a driver injection operation.
/// </summary>
public class DriverInjectionResult
{
    public int AddedCount { get; set; }
    public int FailedCount { get; set; }
    public List<string> Errors { get; set; } = [];
    public bool Success => FailedCount == 0 && Errors.Count == 0;
}

/// <summary>
/// Injects drivers into an offline Windows image using DISM.
/// </summary>
public class DriverInjectionService
{
    private readonly IProcessRunner _processRunner;
    private readonly ILogger<DriverInjectionService> _logger;

    public DriverInjectionService(IProcessRunner processRunner, ILogger<DriverInjectionService> logger)
    {
        _processRunner = processRunner;
        _logger = logger;
    }

    /// <summary>
    /// Injects drivers from a directory into an offline Windows installation.
    /// </summary>
    /// <param name="offlineWindowsPath">Path to the mounted Windows image (e.g., "D:\").</param>
    /// <param name="driverSourcePath">Path to driver directory containing .inf files.</param>
    /// <param name="recurse">Whether to search subdirectories for drivers.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<DriverInjectionResult> InjectDriversAsync(
        string offlineWindowsPath,
        string driverSourcePath,
        bool recurse = true,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Injecting drivers from {DriverPath} into {ImagePath} (recurse: {Recurse})",
            driverSourcePath, offlineWindowsPath, recurse);

        var args = $"/Image:\"{offlineWindowsPath}\" /Add-Driver /Driver:\"{driverSourcePath}\"";
        if (recurse)
            args += " /Recurse";

        var result = await _processRunner.RunAsync("DISM.exe", args, ct);

        var injectionResult = ParseDismOutput(result.StandardOutput);

        if (!result.Success && injectionResult.Errors.Count == 0)
        {
            injectionResult.Errors.Add(
                !string.IsNullOrWhiteSpace(result.StandardError)
                    ? result.StandardError.Trim()
                    : $"DISM exited with code {result.ExitCode}");
        }

        if (injectionResult.Success)
            _logger.LogInformation("Driver injection complete: {Added} driver(s) added", injectionResult.AddedCount);
        else
            _logger.LogWarning("Driver injection completed with errors: {Added} added, {Failed} failed",
                injectionResult.AddedCount, injectionResult.FailedCount);

        return injectionResult;
    }

    /// <summary>
    /// Finds driver .inf files in the specified directory.
    /// </summary>
    public async Task<List<string>> GetDriverFilesAsync(string driverPath, CancellationToken ct = default)
    {
        // Use PowerShell to search for .inf files
        var result = await _processRunner.RunAsync(
            "powershell",
            $"-NoProfile -Command \"Get-ChildItem -Path '{driverPath}' -Filter '*.inf' -Recurse | Select-Object -ExpandProperty FullName\"",
            ct);

        if (!result.Success || string.IsNullOrWhiteSpace(result.StandardOutput))
            return [];

        return result.StandardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();
    }

    /// <summary>
    /// Parses DISM /Add-Driver output to extract success/failure counts.
    /// </summary>
    internal static DriverInjectionResult ParseDismOutput(string output)
    {
        var result = new DriverInjectionResult();

        if (string.IsNullOrWhiteSpace(output))
            return result;

        // Match "Successfully installed X driver(s)"
        var installedMatch = Regex.Match(output, @"(\d+)\s+of\s+(\d+).*installed", RegexOptions.IgnoreCase);
        if (installedMatch.Success)
        {
            result.AddedCount = int.Parse(installedMatch.Groups[1].Value);
            var total = int.Parse(installedMatch.Groups[2].Value);
            result.FailedCount = total - result.AddedCount;
        }
        else
        {
            // Alternative format: "The operation completed successfully."
            if (output.Contains("operation completed successfully", StringComparison.OrdinalIgnoreCase))
            {
                // Count "Installing" lines to estimate driver count
                var installingCount = Regex.Matches(output, @"Installing\s+\d+\s+of\s+\d+", RegexOptions.IgnoreCase).Count;
                if (installingCount > 0)
                    result.AddedCount = installingCount;
                else
                    result.AddedCount = 1; // At least one succeeded
            }
        }

        // Capture any error lines
        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("Error:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("failed", StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add(trimmed);
            }
        }

        return result;
    }
}
