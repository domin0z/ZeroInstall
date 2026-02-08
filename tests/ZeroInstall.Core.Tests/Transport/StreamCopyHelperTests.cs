using System.Text;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Transport;

namespace ZeroInstall.Core.Tests.Transport;

public class StreamCopyHelperTests
{
    [Fact]
    public async Task CopyWithProgressAsync_CopiesAllData()
    {
        var data = Encoding.UTF8.GetBytes("Test data to copy with progress reporting");
        using var source = new MemoryStream(data);
        using var destination = new MemoryStream();

        await StreamCopyHelper.CopyWithProgressAsync(
            source, destination,
            totalBytes: data.Length,
            itemName: "test.txt",
            itemIndex: 1,
            totalItems: 1,
            overallBytesAlreadyTransferred: 0,
            overallTotalBytes: data.Length,
            progress: null);

        destination.ToArray().Should().BeEquivalentTo(data);
    }

    [Fact]
    public async Task CopyWithProgressAsync_ReportsProgress()
    {
        var data = new byte[100_000]; // 100 KB
        new Random(42).NextBytes(data);
        using var source = new MemoryStream(data);
        using var destination = new MemoryStream();

        var progressReports = new List<TransferProgress>();
        var progress = new Progress<TransferProgress>(p => progressReports.Add(p));

        await StreamCopyHelper.CopyWithProgressAsync(
            source, destination,
            totalBytes: data.Length,
            itemName: "large.bin",
            itemIndex: 1,
            totalItems: 1,
            overallBytesAlreadyTransferred: 0,
            overallTotalBytes: data.Length,
            progress);

        // Allow progress events to propagate (Progress<T> posts to SynchronizationContext)
        await Task.Delay(50);

        progressReports.Should().NotBeEmpty();

        // Last report should show all bytes transferred
        var lastReport = progressReports.Last();
        lastReport.CurrentItemBytesTransferred.Should().Be(data.Length);
        lastReport.CurrentItemName.Should().Be("large.bin");
    }

    [Fact]
    public async Task CopyWithProgressAsync_EmptyStream_Succeeds()
    {
        using var source = new MemoryStream();
        using var destination = new MemoryStream();

        await StreamCopyHelper.CopyWithProgressAsync(
            source, destination,
            totalBytes: 0,
            itemName: "empty.txt",
            itemIndex: 1,
            totalItems: 1,
            overallBytesAlreadyTransferred: 0,
            overallTotalBytes: 0,
            progress: null);

        destination.Length.Should().Be(0);
    }

    [Fact]
    public async Task CopyWithProgressAsync_CanBeCancelled()
    {
        // Large enough that cancellation can kick in
        var data = new byte[10_000_000]; // 10 MB
        using var source = new MemoryStream(data);
        using var destination = new MemoryStream();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => StreamCopyHelper.CopyWithProgressAsync(
            source, destination,
            totalBytes: data.Length,
            itemName: "cancel.bin",
            itemIndex: 1,
            totalItems: 1,
            overallBytesAlreadyTransferred: 0,
            overallTotalBytes: data.Length,
            progress: null,
            ct: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task CopyWithProgressAsync_ReportsCorrectItemMetadata()
    {
        var data = new byte[50_000];
        using var source = new MemoryStream(data);
        using var destination = new MemoryStream();

        var progressReports = new List<TransferProgress>();
        var progress = new Progress<TransferProgress>(p => progressReports.Add(p));

        await StreamCopyHelper.CopyWithProgressAsync(
            source, destination,
            totalBytes: data.Length,
            itemName: "item3.dat",
            itemIndex: 3,
            totalItems: 5,
            overallBytesAlreadyTransferred: 200_000,
            overallTotalBytes: 500_000,
            progress);

        await Task.Delay(50);

        progressReports.Should().NotBeEmpty();
        var report = progressReports.First();
        report.CurrentItemName.Should().Be("item3.dat");
        report.CurrentItemIndex.Should().Be(3);
        report.TotalItems.Should().Be(5);
        report.OverallTotalBytes.Should().Be(500_000);
    }
}
