using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ZeroInstall.Agent.Models;
using ZeroInstall.Agent.Services;
using ZeroInstall.Core.Transport;

namespace ZeroInstall.Agent.Tests.Services;

public class AgentTransferServiceTests : IDisposable
{
    private readonly string _sourceDir;
    private readonly string _destDir;

    public AgentTransferServiceTests()
    {
        _sourceDir = Path.Combine(Path.GetTempPath(), $"zim-agent-test-source-{Guid.NewGuid():N}");
        _destDir = Path.Combine(Path.GetTempPath(), $"zim-agent-test-dest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_sourceDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_sourceDir)) Directory.Delete(_sourceDir, true);
        if (Directory.Exists(_destDir)) Directory.Delete(_destDir, true);
    }

    [Fact]
    public async Task RunAsSource_ThrowsIfDirectoryNotFound()
    {
        var options = new AgentOptions
        {
            Role = AgentRole.Source,
            DirectoryPath = @"C:\nonexistent_dir_12345",
            SharedKey = "key"
        };
        var service = new AgentTransferService(options, NullLogger<AgentTransferService>.Instance);

        var act = () => service.RunAsSourceAsync(CancellationToken.None);
        await act.Should().ThrowAsync<DirectoryNotFoundException>();
    }

    [Fact]
    public void EnumerateFiles_ReturnsAllFilesRecursively()
    {
        File.WriteAllText(Path.Combine(_sourceDir, "a.txt"), "aaa");
        var subDir = Path.Combine(_sourceDir, "sub");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "b.txt"), "bbb");

        var files = AgentTransferService.EnumerateFiles(_sourceDir);

        files.Should().HaveCount(2);
        files.Should().Contain(f => f.EndsWith("a.txt"));
        files.Should().Contain(f => f.EndsWith("b.txt"));
    }

    [Fact]
    public void EnumerateFiles_ReturnsEmpty_ForEmptyDirectory()
    {
        var files = AgentTransferService.EnumerateFiles(_sourceDir);
        files.Should().BeEmpty();
    }

    [Fact]
    public void EnumerateFiles_ReturnsSorted()
    {
        File.WriteAllText(Path.Combine(_sourceDir, "z.txt"), "z");
        File.WriteAllText(Path.Combine(_sourceDir, "a.txt"), "a");
        File.WriteAllText(Path.Combine(_sourceDir, "m.txt"), "m");

        var files = AgentTransferService.EnumerateFiles(_sourceDir);

        var names = files.Select(Path.GetFileName).ToList();
        names.Should().BeInAscendingOrder();
    }

    [Fact]
    public void BuildFileEntries_CreatesCorrectRelativePaths()
    {
        var subDir = Path.Combine(_sourceDir, "sub");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(_sourceDir, "root.txt"), "root");
        File.WriteAllText(Path.Combine(subDir, "nested.txt"), "nested");

        var files = AgentTransferService.EnumerateFiles(_sourceDir);
        var entries = AgentTransferService.BuildFileEntries(files, _sourceDir);

        entries.Should().HaveCount(2);
        entries.Should().Contain(e => e.RelativePath == "root.txt");
        entries.Should().Contain(e => e.RelativePath == Path.Combine("sub", "nested.txt"));
    }

    [Fact]
    public void BuildFileEntries_RecordsSizeBytes()
    {
        var content = "Hello, World!";
        var filePath = Path.Combine(_sourceDir, "test.txt");
        File.WriteAllText(filePath, content);

        var files = AgentTransferService.EnumerateFiles(_sourceDir);
        var entries = AgentTransferService.BuildFileEntries(files, _sourceDir);

        entries.Should().ContainSingle();
        entries[0].SizeBytes.Should().Be(new FileInfo(filePath).Length);
    }

    [Fact]
    public void BuildManifest_SetsCorrectProperties()
    {
        var entries = new List<FileEntry>
        {
            new() { FullPath = @"C:\test\a.txt", RelativePath = "a.txt", SizeBytes = 100 },
            new() { FullPath = @"C:\test\b.txt", RelativePath = "b.txt", SizeBytes = 200 }
        };

        var manifest = AgentTransferService.BuildManifest(entries);

        manifest.SourceHostname.Should().Be(Environment.MachineName);
        manifest.TransportMethod.Should().Be(Core.Enums.TransportMethod.DirectWiFi);
        manifest.Items.Should().HaveCount(2);
        manifest.Items[0].DisplayName.Should().Be("a.txt");
        manifest.Items[0].IsSelected.Should().BeTrue();
        manifest.Items[1].EstimatedSizeBytes.Should().Be(200);
        manifest.TotalEstimatedSizeBytes.Should().Be(300);
    }

    [Fact]
    public async Task SourceAndDestination_RejectedKey_ThrowsUnauthorized()
    {
        // Create a test file on the source side
        File.WriteAllText(Path.Combine(_sourceDir, "test.txt"), "content");

        var port = GetFreePort();
        var sourceOptions = new AgentOptions
        {
            Role = AgentRole.Source,
            Port = port,
            SharedKey = "correct-key",
            DirectoryPath = _sourceDir
        };
        var destOptions = new AgentOptions
        {
            Role = AgentRole.Destination,
            Port = port,
            SharedKey = "wrong-key",
            DirectoryPath = _destDir,
            PeerAddress = "127.0.0.1"
        };

        var sourceService = new AgentTransferService(sourceOptions, NullLogger<AgentTransferService>.Instance);
        var destService = new AgentTransferService(destOptions, NullLogger<AgentTransferService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var sourceTask = sourceService.RunAsSourceAsync(cts.Token);
        await Task.Delay(200); // Let server start listening
        var destTask = destService.RunAsDestinationAsync(cts.Token);

        // Source should throw because the key is wrong
        var sourceAct = () => sourceTask;
        await sourceAct.Should().ThrowAsync<UnauthorizedAccessException>();

        // Destination should throw because response says rejected
        var destAct = () => destTask;
        await destAct.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task FullTransfer_SingleFile_OverLoopback()
    {
        var content = "Hello from source!";
        File.WriteAllText(Path.Combine(_sourceDir, "hello.txt"), content);

        var port = GetFreePort();
        var sourceOptions = new AgentOptions
        {
            Role = AgentRole.Source,
            Port = port,
            SharedKey = "test-key",
            DirectoryPath = _sourceDir
        };
        var destOptions = new AgentOptions
        {
            Role = AgentRole.Destination,
            Port = port,
            SharedKey = "test-key",
            DirectoryPath = _destDir,
            PeerAddress = "127.0.0.1"
        };

        var sourceService = new AgentTransferService(sourceOptions, NullLogger<AgentTransferService>.Instance);
        var destService = new AgentTransferService(destOptions, NullLogger<AgentTransferService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var sourceTask = sourceService.RunAsSourceAsync(cts.Token);
        await Task.Delay(200);
        var destTask = destService.RunAsDestinationAsync(cts.Token);

        await Task.WhenAll(sourceTask, destTask);

        var destFile = Path.Combine(_destDir, "hello.txt");
        File.Exists(destFile).Should().BeTrue();
        File.ReadAllText(destFile).Should().Be(content);
    }

    [Fact]
    public async Task FullTransfer_MultipleFiles_WithSubdirs()
    {
        // Create source structure
        File.WriteAllText(Path.Combine(_sourceDir, "root.txt"), "root content");
        var subDir = Path.Combine(_sourceDir, "subdir");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "nested.txt"), "nested content");
        File.WriteAllText(Path.Combine(subDir, "data.bin"), new string('X', 1024));

        var port = GetFreePort();
        var sourceOptions = new AgentOptions
        {
            Role = AgentRole.Source,
            Port = port,
            SharedKey = "multi-key",
            DirectoryPath = _sourceDir
        };
        var destOptions = new AgentOptions
        {
            Role = AgentRole.Destination,
            Port = port,
            SharedKey = "multi-key",
            DirectoryPath = _destDir,
            PeerAddress = "127.0.0.1"
        };

        var sourceService = new AgentTransferService(sourceOptions, NullLogger<AgentTransferService>.Instance);
        var destService = new AgentTransferService(destOptions, NullLogger<AgentTransferService>.Instance);

        // Track progress events
        var progressEvents = new List<Core.Models.TransferProgress>();
        destService.ProgressChanged += p => progressEvents.Add(p);

        var statusEvents = new List<string>();
        destService.StatusChanged += s => statusEvents.Add(s);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        var sourceTask = sourceService.RunAsSourceAsync(cts.Token);
        await Task.Delay(200);
        var destTask = destService.RunAsDestinationAsync(cts.Token);

        await Task.WhenAll(sourceTask, destTask);

        // Verify all files transferred
        File.Exists(Path.Combine(_destDir, "root.txt")).Should().BeTrue();
        File.ReadAllText(Path.Combine(_destDir, "root.txt")).Should().Be("root content");

        File.Exists(Path.Combine(_destDir, "subdir", "nested.txt")).Should().BeTrue();
        File.ReadAllText(Path.Combine(_destDir, "subdir", "nested.txt")).Should().Be("nested content");

        File.Exists(Path.Combine(_destDir, "subdir", "data.bin")).Should().BeTrue();
        File.ReadAllText(Path.Combine(_destDir, "subdir", "data.bin")).Should().HaveLength(1024);

        // Verify events fired
        progressEvents.Should().NotBeEmpty();
        statusEvents.Should().Contain("Transfer complete");
    }

    [Fact]
    public async Task FullTransfer_EmptyDirectory_CompletesSuccessfully()
    {
        // Source dir is empty (no files)
        var port = GetFreePort();
        var sourceOptions = new AgentOptions
        {
            Role = AgentRole.Source,
            Port = port,
            SharedKey = "empty-key",
            DirectoryPath = _sourceDir
        };
        var destOptions = new AgentOptions
        {
            Role = AgentRole.Destination,
            Port = port,
            SharedKey = "empty-key",
            DirectoryPath = _destDir,
            PeerAddress = "127.0.0.1"
        };

        var sourceService = new AgentTransferService(sourceOptions, NullLogger<AgentTransferService>.Instance);
        var destService = new AgentTransferService(destOptions, NullLogger<AgentTransferService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var sourceTask = sourceService.RunAsSourceAsync(cts.Token);
        await Task.Delay(200);
        var destTask = destService.RunAsDestinationAsync(cts.Token);

        await Task.WhenAll(sourceTask, destTask);

        Directory.Exists(_destDir).Should().BeTrue();
        Directory.GetFiles(_destDir, "*", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Fact]
    public async Task SourceStatusChanged_FiresExpectedEvents()
    {
        File.WriteAllText(Path.Combine(_sourceDir, "test.txt"), "content");

        var port = GetFreePort();
        var sourceOptions = new AgentOptions
        {
            Role = AgentRole.Source,
            Port = port,
            SharedKey = "status-key",
            DirectoryPath = _sourceDir
        };
        var destOptions = new AgentOptions
        {
            Role = AgentRole.Destination,
            Port = port,
            SharedKey = "status-key",
            DirectoryPath = _destDir,
            PeerAddress = "127.0.0.1"
        };

        var sourceService = new AgentTransferService(sourceOptions, NullLogger<AgentTransferService>.Instance);
        var destService = new AgentTransferService(destOptions, NullLogger<AgentTransferService>.Instance);

        var sourceStatuses = new List<string>();
        sourceService.StatusChanged += s => sourceStatuses.Add(s);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var sourceTask = sourceService.RunAsSourceAsync(cts.Token);
        await Task.Delay(200);
        var destTask = destService.RunAsDestinationAsync(cts.Token);

        await Task.WhenAll(sourceTask, destTask);

        sourceStatuses.Should().Contain("Waiting for connection...");
        sourceStatuses.Should().Contain("Authenticating...");
        sourceStatuses.Should().Contain("Transfer complete");
        sourceStatuses.Should().Contain(s => s.Contains("Connected to"));
    }

    private static int GetFreePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
