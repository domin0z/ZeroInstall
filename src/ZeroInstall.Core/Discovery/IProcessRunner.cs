namespace ZeroInstall.Core.Discovery;

/// <summary>
/// Abstraction over running external processes (winget, choco, netsh, etc.) for testability.
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Runs a process and returns its stdout.
    /// </summary>
    Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken ct = default);
}

public class ProcessResult
{
    public int ExitCode { get; set; }
    public string StandardOutput { get; set; } = string.Empty;
    public string StandardError { get; set; } = string.Empty;
    public bool Success => ExitCode == 0;
}

/// <summary>
/// Real implementation that launches Windows processes.
/// </summary>
public class WindowsProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken ct = default)
    {
        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);

        return new ProcessResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = await stdoutTask,
            StandardError = await stderrTask
        };
    }
}
