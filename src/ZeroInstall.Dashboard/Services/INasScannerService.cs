namespace ZeroInstall.Dashboard.Services;

public interface INasScannerService
{
    Task ScanAsync(CancellationToken ct = default);
}
