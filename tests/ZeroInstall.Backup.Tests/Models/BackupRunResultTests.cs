using ZeroInstall.Backup.Enums;
using ZeroInstall.Backup.Models;

namespace ZeroInstall.Backup.Tests.Models;

public class BackupRunResultTests
{
    [Fact]
    public void Duration_ComputedFromStartAndCompleted()
    {
        var result = new BackupRunResult
        {
            StartedUtc = new DateTime(2026, 2, 10, 2, 0, 0, DateTimeKind.Utc),
            CompletedUtc = new DateTime(2026, 2, 10, 2, 15, 30, DateTimeKind.Utc)
        };

        result.Duration.Should().Be(TimeSpan.FromSeconds(930));
    }

    [Fact]
    public void Defaults_AreReasonable()
    {
        var result = new BackupRunResult();

        result.RunId.Should().BeEmpty();
        result.BackupType.Should().Be("file");
        result.ResultType.Should().Be(BackupRunResultType.Success);
        result.FilesScanned.Should().Be(0);
        result.FilesUploaded.Should().Be(0);
        result.FilesFailed.Should().Be(0);
        result.BytesTransferred.Should().Be(0);
        result.Errors.Should().BeEmpty();
    }
}
