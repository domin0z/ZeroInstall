namespace ZeroInstall.Dashboard.Services;

public class DashboardConfiguration
{
    public string DatabasePath { get; set; } = "dashboard.db";
    public string? NasSftpHost { get; set; }
    public int NasSftpPort { get; set; } = 22;
    public string? NasSftpUser { get; set; }
    public string? NasSftpPassword { get; set; }
    public string? NasSftpKeyPath { get; set; }
    public string NasSftpBasePath { get; set; } = "/backups/zim";
    public int NasScanIntervalMinutes { get; set; } = 5;
    public string ApiKey { get; set; } = Guid.NewGuid().ToString("N");
    public int ListenPort { get; set; } = 5180;
}
