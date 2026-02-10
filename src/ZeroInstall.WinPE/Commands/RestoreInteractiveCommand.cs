using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Models;
using ZeroInstall.WinPE.Infrastructure;
using ZeroInstall.WinPE.Services;

namespace ZeroInstall.WinPE.Commands;

/// <summary>
/// Interactive TUI workflow for restoring a disk image.
/// </summary>
internal static class RestoreInteractiveCommand
{
    public static async Task<int> RunAsync(IHost host, CancellationToken ct)
    {
        WinPeConsoleUI.WriteHeader();

        var imageBrowser = host.Services.GetRequiredService<ImageBrowserService>();
        var diskEnum = host.Services.GetRequiredService<DiskEnumerationService>();
        var orchestrator = host.Services.GetRequiredService<RestoreOrchestrator>();

        // Step 1: Prompt for search path
        Console.Write("  Enter path to search for images (e.g., D:\\): ");
        var searchPath = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(searchPath))
        {
            WinPeConsoleUI.WriteError("No path provided.");
            return 1;
        }

        // Step 2: Find images
        Console.WriteLine("  Scanning for disk images...");
        var images = await imageBrowser.FindImagesAsync(searchPath, ct);

        if (images.Count == 0)
        {
            WinPeConsoleUI.WriteError($"No disk images found in {searchPath}");
            return 1;
        }

        // Step 3: Select image
        var imageOptions = images.Select(img =>
        {
            var name = Path.GetFileName(img.ImagePath);
            var size = WinPeConsoleUI.FormatBytes(img.FileSizeBytes);
            var source = img.Metadata?.SourceHostname ?? "unknown";
            return $"{name} ({size}) — from {source}";
        }).ToArray();

        var imageIndex = WinPeConsoleUI.ShowMenu("Select disk image to restore:", imageOptions);
        var selectedImage = images[imageIndex];

        // Step 4: Show image metadata
        if (selectedImage.Metadata != null)
        {
            WinPeConsoleUI.ShowImageInfo(selectedImage.Metadata);
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine($"  Image: {selectedImage.ImagePath}");
            Console.WriteLine($"  Size:  {WinPeConsoleUI.FormatBytes(selectedImage.FileSizeBytes)}");
            WinPeConsoleUI.WriteWarning("No metadata file found — source information unavailable.");
        }

        // Step 5: Enumerate disks and volumes
        Console.WriteLine();
        Console.WriteLine("  Enumerating target disks...");
        var disks = await diskEnum.GetDisksAsync(ct);
        var volumes = await diskEnum.GetVolumesAsync(ct);

        if (disks.Count == 0)
        {
            WinPeConsoleUI.WriteError("No disks detected on this system.");
            return 1;
        }

        WinPeConsoleUI.ShowDiskTable(disks);
        WinPeConsoleUI.ShowVolumeTable(volumes);

        // Step 6: Select target volume
        var volumeOptions = volumes
            .Where(v => !string.IsNullOrEmpty(v.DriveLetter))
            .Select(v =>
            {
                var label = string.IsNullOrEmpty(v.Label) ? "(no label)" : v.Label;
                return $"{v.DriveLetter}: — {label} ({WinPeConsoleUI.FormatBytes(v.SizeBytes)}, {v.FileSystem})";
            }).ToArray();

        if (volumeOptions.Length == 0)
        {
            WinPeConsoleUI.WriteError("No volumes with drive letters found.");
            return 1;
        }

        var volumeIndex = WinPeConsoleUI.ShowMenu("Select target volume for restore:", volumeOptions);
        var targetVolume = volumes.Where(v => !string.IsNullOrEmpty(v.DriveLetter)).ElementAt(volumeIndex);
        var targetPath = $"{targetVolume.DriveLetter}:\\";

        // Step 7: Validate space
        var requiredBytes = selectedImage.Metadata?.SourceVolumeUsedBytes ?? selectedImage.FileSizeBytes;
        if (targetVolume.SizeBytes < requiredBytes)
        {
            WinPeConsoleUI.WriteWarning(
                $"Target volume ({WinPeConsoleUI.FormatBytes(targetVolume.SizeBytes)}) may be too small " +
                $"for source data ({WinPeConsoleUI.FormatBytes(requiredBytes)}).");

            if (!WinPeConsoleUI.PromptYesNo("Continue anyway?"))
                return 1;
        }

        // Step 8: Verify options
        var skipVerify = !WinPeConsoleUI.PromptYesNo("Verify image integrity before restoring?");

        // Step 9: Driver injection option
        string? driverPath = null;
        if (WinPeConsoleUI.PromptYesNo("Inject drivers after restore?"))
        {
            Console.Write("  Enter path to driver directory: ");
            driverPath = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(driverPath))
            {
                WinPeConsoleUI.WriteWarning("No driver path provided — skipping driver injection.");
                driverPath = null;
            }
        }

        // Step 10: Final confirmation
        Console.WriteLine();
        Console.WriteLine("  ====================================");
        Console.WriteLine("  RESTORE SUMMARY");
        Console.WriteLine("  ====================================");
        Console.WriteLine($"  Image:   {Path.GetFileName(selectedImage.ImagePath)}");
        Console.WriteLine($"  Target:  {targetPath}");
        Console.WriteLine($"  Verify:  {(!skipVerify ? "Yes" : "No")}");
        Console.WriteLine($"  Drivers: {(driverPath != null ? driverPath : "None")}");
        Console.WriteLine("  ====================================");
        Console.WriteLine();
        WinPeConsoleUI.WriteWarning("WARNING: This will OVERWRITE all data on the target volume!");

        if (!WinPeConsoleUI.PromptYesNo("Proceed with restore?"))
        {
            Console.WriteLine("  Restore cancelled.");
            return 1;
        }

        // Step 11: Run restore
        Console.WriteLine();
        var progress = new Progress<TransferProgress>(WinPeConsoleUI.WriteProgress);
        var options = new RestoreOptions
        {
            SkipVerify = skipVerify,
            DriverPath = driverPath,
            Recurse = true
        };

        var result = await orchestrator.RunRestoreAsync(
            selectedImage.ImagePath, targetPath, options, progress, ct);

        // Step 12: Show results
        Console.WriteLine();
        if (result.Success)
        {
            WinPeConsoleUI.WriteSuccess($"Restore completed in {WinPeConsoleUI.FormatTimeSpan(result.Duration)}");

            if (result.DriverResult != null)
            {
                if (result.DriverResult.Success)
                    WinPeConsoleUI.WriteSuccess($"Drivers injected: {result.DriverResult.AddedCount} driver(s) added");
                else
                {
                    WinPeConsoleUI.WriteWarning(
                        $"Driver injection: {result.DriverResult.AddedCount} added, {result.DriverResult.FailedCount} failed");
                    foreach (var error in result.DriverResult.Errors)
                        WinPeConsoleUI.WriteError($"  {error}");
                }
            }

            Console.WriteLine();
            Console.WriteLine("  You may now reboot into the restored operating system.");
        }
        else
        {
            WinPeConsoleUI.WriteError($"Restore failed: {result.Error}");
            return 1;
        }

        return 0;
    }
}
