using Microsoft.EntityFrameworkCore;
using ZeroInstall.Dashboard.Data.Entities;

namespace ZeroInstall.Dashboard.Data;

public class DashboardDbContext : DbContext
{
    public DbSet<JobRecord> Jobs => Set<JobRecord>();
    public DbSet<JobReportRecord> Reports => Set<JobReportRecord>();
    public DbSet<BackupStatusRecord> BackupStatuses => Set<BackupStatusRecord>();
    public DbSet<AlertRecord> Alerts => Set<AlertRecord>();

    public DashboardDbContext(DbContextOptions<DashboardDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<JobRecord>(entity =>
        {
            entity.HasIndex(e => e.JobId).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.TechnicianName);
        });

        modelBuilder.Entity<JobReportRecord>(entity =>
        {
            entity.HasIndex(e => e.ReportId).IsUnique();
            entity.HasIndex(e => e.JobId);
        });

        modelBuilder.Entity<BackupStatusRecord>(entity =>
        {
            entity.HasIndex(e => e.CustomerId).IsUnique();
        });

        modelBuilder.Entity<AlertRecord>(entity =>
        {
            entity.HasIndex(e => e.IsActive);
        });
    }
}
