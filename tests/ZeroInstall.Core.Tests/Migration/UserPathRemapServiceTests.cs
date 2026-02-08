using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;
using NSubstitute;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Migration;
using ZeroInstall.Core.Models;

namespace ZeroInstall.Core.Tests.Migration;

public class UserPathRemapServiceTests : IDisposable
{
    private readonly IProcessRunner _processRunner = Substitute.For<IProcessRunner>();
    private readonly IFileSystemAccessor _fileSystem = Substitute.For<IFileSystemAccessor>();
    private readonly IRegistryAccessor _registry = Substitute.For<IRegistryAccessor>();
    private readonly UserPathRemapService _service;
    private readonly string _tempDir;

    private static readonly UserMapping TestMapping = new()
    {
        SourceUser = new UserProfile
        {
            Username = "Bill",
            ProfilePath = @"C:\Users\Bill"
        },
        DestinationUsername = "William",
        DestinationProfilePath = @"C:\Users\William"
    };

    public UserPathRemapServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "zim-remap-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _service = new UserPathRemapService(
            _processRunner, _fileSystem, _registry,
            NullLogger<UserPathRemapService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    #region RemapPathsAsync

    [Fact]
    public async Task RemapPathsAsync_SkipsWhenNoRemappingNeeded()
    {
        var mapping = new UserMapping
        {
            SourceUser = new UserProfile { Username = "Same", ProfilePath = @"C:\Users\Same" },
            DestinationUsername = "Same",
            DestinationProfilePath = @"C:\Users\Same"
        };

        await _service.RemapPathsAsync(mapping, _tempDir);

        // Should not call any filesystem or process operations
        _fileSystem.DidNotReceive().DirectoryExists(Arg.Any<string>());
    }

    [Fact]
    public async Task RemapPathsAsync_InvokesAllSubMethods()
    {
        _fileSystem.DirectoryExists(Arg.Any<string>()).Returns(false);
        _registry.GetValueNames(Arg.Any<RegistryHive>(), Arg.Any<RegistryView>(), Arg.Any<string>())
            .Returns(Array.Empty<string>());
        _processRunner.RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1 });

        await _service.RemapPathsAsync(TestMapping, _tempDir);

        // Verify that it tried to check directories (shortcuts, configs, taskbar, recent)
        _fileSystem.Received().DirectoryExists(Arg.Any<string>());
    }

    #endregion

    #region RemapShortcutsAsync

    [Fact]
    public async Task RemapShortcutsAsync_ProcessesLnkFiles()
    {
        _fileSystem.DirectoryExists(_tempDir).Returns(true);
        _fileSystem.GetFiles(_tempDir, "*.lnk", SearchOption.AllDirectories)
            .Returns(new[] { Path.Combine(_tempDir, "test.lnk") });

        _processRunner.RunAsync("powershell", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0, StandardOutput = "True" });

        await _service.RemapShortcutsAsync(TestMapping, _tempDir);

        await _processRunner.Received().RunAsync("powershell",
            Arg.Is<string>(s => s.Contains("WScript.Shell")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemapShortcutsAsync_SkipsWhenDirectoryNotExists()
    {
        _fileSystem.DirectoryExists("/nonexistent").Returns(false);

        await _service.RemapShortcutsAsync(TestMapping, "/nonexistent");

        _fileSystem.DidNotReceive().GetFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>());
    }

    #endregion

    #region RemapConfigFilesAsync

    [Fact]
    public async Task RemapConfigFilesAsync_RemapsJsonFile()
    {
        var configFile = Path.Combine(_tempDir, "settings.json");
        await File.WriteAllTextAsync(configFile, "{\"path\": \"C:\\Users\\Bill\\AppData\\stuff\"}");

        _fileSystem.DirectoryExists(_tempDir).Returns(true);
        _fileSystem.GetFiles(_tempDir, "*.json", SearchOption.AllDirectories)
            .Returns(new[] { configFile });
        _fileSystem.GetFiles(_tempDir, Arg.Is<string>(s => s != "*.json"), SearchOption.AllDirectories)
            .Returns(Array.Empty<string>());

        await _service.RemapConfigFilesAsync(TestMapping, _tempDir);

        var content = await File.ReadAllTextAsync(configFile);
        content.Should().Contain(@"C:\Users\William");
        content.Should().NotContain(@"C:\Users\Bill");
    }

    [Fact]
    public async Task RemapConfigFilesAsync_RemapsIniFile()
    {
        var iniFile = Path.Combine(_tempDir, "app.ini");
        await File.WriteAllTextAsync(iniFile, "[Settings]\nPath=C:\\Users\\Bill\\Documents\n");

        _fileSystem.DirectoryExists(_tempDir).Returns(true);
        _fileSystem.GetFiles(_tempDir, "*.ini", SearchOption.AllDirectories)
            .Returns(new[] { iniFile });
        _fileSystem.GetFiles(_tempDir, Arg.Is<string>(s => s != "*.ini"), SearchOption.AllDirectories)
            .Returns(Array.Empty<string>());

        await _service.RemapConfigFilesAsync(TestMapping, _tempDir);

        var content = await File.ReadAllTextAsync(iniFile);
        content.Should().Contain(@"C:\Users\William\Documents");
    }

    [Fact]
    public async Task RemapConfigFilesAsync_LeavesFileUnchanged_WhenNoPathMatch()
    {
        var configFile = Path.Combine(_tempDir, "other.xml");
        var originalContent = "<config><path>C:\\Program Files\\App</path></config>";
        await File.WriteAllTextAsync(configFile, originalContent);

        _fileSystem.DirectoryExists(_tempDir).Returns(true);
        _fileSystem.GetFiles(_tempDir, "*.xml", SearchOption.AllDirectories)
            .Returns(new[] { configFile });
        _fileSystem.GetFiles(_tempDir, Arg.Is<string>(s => s != "*.xml"), SearchOption.AllDirectories)
            .Returns(Array.Empty<string>());

        await _service.RemapConfigFilesAsync(TestMapping, _tempDir);

        var content = await File.ReadAllTextAsync(configFile);
        content.Should().Be(originalContent);
    }

    [Fact]
    public async Task RemapConfigFilesAsync_SkipsWhenDirectoryNotExists()
    {
        _fileSystem.DirectoryExists("/nonexistent").Returns(false);

        await _service.RemapConfigFilesAsync(TestMapping, "/nonexistent");

        _fileSystem.DidNotReceive().GetFiles(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<SearchOption>());
    }

    #endregion

    #region RemapUrlFilesAsync

    [Fact]
    public async Task RemapUrlFileAsync_RemapsUrlContent()
    {
        var urlFile = Path.Combine(_tempDir, "shortcut.url");
        await File.WriteAllTextAsync(urlFile, "[InternetShortcut]\nURL=C:\\Users\\Bill\\Documents\\file.html\n");

        await _service.RemapUrlFileAsync(urlFile, TestMapping, CancellationToken.None);

        var content = await File.ReadAllTextAsync(urlFile);
        content.Should().Contain("William");
        content.Should().NotContain("Bill");
    }

    [Fact]
    public async Task RemapUrlFileAsync_LeavesUnchanged_WhenNoMatch()
    {
        var urlFile = Path.Combine(_tempDir, "web.url");
        var originalContent = "[InternetShortcut]\nURL=https://example.com\n";
        await File.WriteAllTextAsync(urlFile, originalContent);

        await _service.RemapUrlFileAsync(urlFile, TestMapping, CancellationToken.None);

        var content = await File.ReadAllTextAsync(urlFile);
        content.Should().Be(originalContent);
    }

    #endregion

    #region RemapRegistryPathsAsync

    [Fact]
    public async Task RemapEnvironmentVariablesAsync_RemapsMatchingValues()
    {
        _registry.GetValueNames(RegistryHive.CurrentUser, RegistryView.Default, "Environment")
            .Returns(new[] { "TOOL_PATH" });

        _registry.GetStringValue(RegistryHive.CurrentUser, RegistryView.Default, "Environment", "TOOL_PATH")
            .Returns(@"C:\Users\Bill\Tools");

        _processRunner.RunAsync("reg", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        await _service.RemapEnvironmentVariablesAsync(TestMapping, CancellationToken.None);

        await _processRunner.Received().RunAsync("reg",
            Arg.Is<string>(s => s.Contains("William") && s.Contains("TOOL_PATH")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemapEnvironmentVariablesAsync_SkipsNonMatchingValues()
    {
        _registry.GetValueNames(RegistryHive.CurrentUser, RegistryView.Default, "Environment")
            .Returns(new[] { "JAVA_HOME" });

        _registry.GetStringValue(RegistryHive.CurrentUser, RegistryView.Default, "Environment", "JAVA_HOME")
            .Returns(@"C:\Program Files\Java");

        await _service.RemapEnvironmentVariablesAsync(TestMapping, CancellationToken.None);

        await _processRunner.DidNotReceive().RunAsync("reg",
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemapMruRegistryEntriesAsync_ExportsAndReimports()
    {
        // The export creates a temp file; we simulate creating it with user path content
        _processRunner.RunAsync("reg", Arg.Is<string>(s => s.Contains("export") && s.Contains("ComDlg32")), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var args = callInfo.ArgAt<string>(1);
                // Extract the temp file path from: export "HKCU\...\ComDlg32" "tempfile" /y
                var parts = args.Split('"');
                // parts: [export , HKCU\...\ComDlg32,  , tempfile,  /y]
                var filePath = parts.Length >= 4 ? parts[3] : "";
                if (!string.IsNullOrEmpty(filePath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                    // .reg files contain double-backslash paths in string values
                    File.WriteAllText(filePath, "[HKCU\\Software]\r\n\"LastPath\"=\"C:\\\\Users\\\\Bill\\\\Documents\"");
                }
                return new ProcessResult { ExitCode = 0 };
            });

        _processRunner.RunAsync("reg", Arg.Is<string>(s => s.Contains("import")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        await _service.RemapMruRegistryEntriesAsync(TestMapping, CancellationToken.None);

        await _processRunner.Received().RunAsync("reg",
            Arg.Is<string>(s => s.Contains("import")),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region RemapPinnedTaskbarItemsAsync

    [Fact]
    public async Task RemapPinnedTaskbarItemsAsync_DelegatesToRemapShortcuts()
    {
        var taskbarDir = Path.Combine(TestMapping.DestinationProfilePath,
            "AppData", "Roaming", "Microsoft", "Internet Explorer", "Quick Launch", "User Pinned", "TaskBar");

        _fileSystem.DirectoryExists(taskbarDir).Returns(true);
        _fileSystem.GetFiles(taskbarDir, "*.lnk", SearchOption.AllDirectories)
            .Returns(Array.Empty<string>());

        await _service.RemapPinnedTaskbarItemsAsync(TestMapping, CancellationToken.None);

        _fileSystem.Received().DirectoryExists(taskbarDir);
    }

    #endregion

    #region RemapRecentFilesAsync

    [Fact]
    public async Task RemapRecentFilesAsync_DelegatesToRemapShortcuts()
    {
        var recentDir = Path.Combine(TestMapping.DestinationProfilePath,
            "AppData", "Roaming", "Microsoft", "Windows", "Recent");

        _fileSystem.DirectoryExists(recentDir).Returns(true);
        _fileSystem.GetFiles(recentDir, "*.lnk", SearchOption.AllDirectories)
            .Returns(Array.Empty<string>());

        await _service.RemapRecentFilesAsync(TestMapping, CancellationToken.None);

        _fileSystem.Received().DirectoryExists(recentDir);
    }

    #endregion

    #region Static Helpers

    [Fact]
    public void ReplacePathPrefix_ReplacesCaseInsensitive()
    {
        var result = UserPathRemapService.ReplacePathPrefix(
            @"c:\users\bill\documents\file.txt",
            @"C:\Users\Bill",
            @"C:\Users\William");

        result.Should().Be(@"C:\Users\William\documents\file.txt");
    }

    [Fact]
    public void ReplacePathPrefix_HandlesMultipleOccurrences()
    {
        var result = UserPathRemapService.ReplacePathPrefix(
            @"path1=C:\Users\Bill\A;path2=C:\Users\Bill\B",
            @"C:\Users\Bill",
            @"C:\Users\William");

        result.Should().Be(@"path1=C:\Users\William\A;path2=C:\Users\William\B");
    }

    [Fact]
    public void ContainsUserPath_ReturnsTrueForMatch()
    {
        UserPathRemapService.ContainsUserPath(
            @"Some text with C:\Users\Bill\Documents in it",
            @"C:\Users\Bill").Should().BeTrue();
    }

    [Fact]
    public void ContainsUserPath_CaseInsensitive()
    {
        UserPathRemapService.ContainsUserPath(
            @"c:\users\bill\documents",
            @"C:\Users\Bill").Should().BeTrue();
    }

    [Fact]
    public void ContainsUserPath_ReturnsFalseForNoMatch()
    {
        UserPathRemapService.ContainsUserPath(
            @"C:\Program Files\App",
            @"C:\Users\Bill").Should().BeFalse();
    }

    #endregion
}
