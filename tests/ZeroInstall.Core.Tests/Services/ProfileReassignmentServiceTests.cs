using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;
using NSubstitute;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;

namespace ZeroInstall.Core.Tests.Services;

public class ProfileReassignmentServiceTests
{
    private readonly IProcessRunner _processRunner = Substitute.For<IProcessRunner>();
    private readonly IRegistryAccessor _registry = Substitute.For<IRegistryAccessor>();
    private readonly ProfileReassignmentService _service;

    private const string OldSid = "S-1-5-21-123-456-789-1001";
    private const string NewSid = "S-1-5-21-999-888-777-4523";
    private const string ProfilePath = @"C:\Users\Bill";

    public ProfileReassignmentServiceTests()
    {
        _service = new ProfileReassignmentService(
            _processRunner, _registry, NullLogger<ProfileReassignmentService>.Instance);
    }

    #region ReassignProfileAsync

    [Fact]
    public async Task ReassignProfileAsync_ExportFails_ReturnsFalse()
    {
        _processRunner.RunAsync("reg", Arg.Is<string>(s => s.Contains("export") && s.Contains(OldSid)), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = -1, StandardError = "Key not found" });

        var (success, message) = await _service.ReassignProfileAsync(OldSid, NewSid, ProfilePath);

        success.Should().BeFalse();
        message.Should().Contain("Failed to export");
    }

    [Fact]
    public async Task ReassignProfileAsync_ExportSucceeds_CallsIcaclsForAcl()
    {
        SetupSuccessfulRegExport();

        var (success, message) = await _service.ReassignProfileAsync(OldSid, NewSid, ProfilePath);

        success.Should().BeTrue();
        await _processRunner.Received().RunAsync("icacls",
            Arg.Is<string>(s => s.Contains(NewSid) && s.Contains("/grant")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReassignProfileAsync_ExportSucceeds_CallsIcaclsForOwner()
    {
        SetupSuccessfulRegExport();

        var (success, message) = await _service.ReassignProfileAsync(OldSid, NewSid, ProfilePath);

        success.Should().BeTrue();
        await _processRunner.Received().RunAsync("icacls",
            Arg.Is<string>(s => s.Contains("/setowner") && s.Contains(NewSid)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReassignProfileAsync_Success_MessageContainsSids()
    {
        SetupSuccessfulRegExport();

        var (success, message) = await _service.ReassignProfileAsync(OldSid, NewSid, ProfilePath);

        success.Should().BeTrue();
        message.Should().Contain(OldSid);
        message.Should().Contain(NewSid);
    }

    private void SetupSuccessfulRegExport()
    {
        var tempDir = Path.GetTempPath();

        // Create the temp reg file upfront so ImportWithSidReplace can read it
        var regFile = Path.Combine(tempDir, $"zim_profile_{OldSid}.reg");
        File.WriteAllText(regFile, $"[HKLM\\Test\\{OldSid}]\n\"Value\"=\"test\"");

        // Create the NTUSER.DAT hive export temp file
        var hiveTempFile = Path.Combine(tempDir, "zim_ntuser_temp.reg");
        File.WriteAllText(hiveTempFile, $"[HKU\\TEMP_ZIM\\{OldSid}]\n\"Data\"=\"value\"");

        // All process calls succeed
        _processRunner.RunAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });
    }

    #endregion

    #region RenameProfileFolderAsync

    [Fact]
    public async Task RenameProfileFolderAsync_Success_ReturnsTrue()
    {
        _processRunner.RunAsync("cmd", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        _registry.GetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry64,
            Arg.Any<string>()).Returns(new[] { OldSid });
        _registry.GetStringValue(RegistryHive.LocalMachine, RegistryView.Registry64,
            Arg.Any<string>(), "ProfileImagePath").Returns(ProfilePath);

        var (success, message) = await _service.RenameProfileFolderAsync(ProfilePath, "William");

        success.Should().BeTrue();
        message.Should().Contain("William");
    }

    [Fact]
    public async Task RenameProfileFolderAsync_RenameFails_ReturnsFalse()
    {
        _processRunner.RunAsync("cmd", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = -1, StandardError = "Access denied" });

        var (success, message) = await _service.RenameProfileFolderAsync(ProfilePath, "William");

        success.Should().BeFalse();
        message.Should().Contain("Failed to rename");
    }

    [Fact]
    public async Task RenameProfileFolderAsync_UpdatesRegistryProfileImagePath()
    {
        _processRunner.RunAsync("cmd", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        _registry.GetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry64,
            Arg.Any<string>()).Returns(new[] { OldSid });
        _registry.GetStringValue(RegistryHive.LocalMachine, RegistryView.Registry64,
            Arg.Any<string>(), "ProfileImagePath").Returns(ProfilePath);

        await _service.RenameProfileFolderAsync(ProfilePath, "William");

        _registry.Received().SetStringValue(
            RegistryHive.LocalMachine, RegistryView.Registry64,
            Arg.Any<string>(), "ProfileImagePath", @"C:\Users\William");
    }

    [Fact]
    public async Task RenameProfileFolderAsync_CallsRenCommand()
    {
        _processRunner.RunAsync("cmd", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        _registry.GetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry64,
            Arg.Any<string>()).Returns(Array.Empty<string>());

        await _service.RenameProfileFolderAsync(ProfilePath, "William");

        await _processRunner.Received().RunAsync("cmd",
            Arg.Is<string>(s => s.Contains("ren") && s.Contains("William")),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region SetSidHistoryAsync

    [Fact]
    public async Task SetSidHistoryAsync_Success_ReturnsTrue()
    {
        _processRunner.RunAsync("powershell", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        var creds = new DomainCredentials { Domain = "corp.local", Username = "admin", Password = "pass" };
        var (success, message) = await _service.SetSidHistoryAsync(NewSid, OldSid, creds);

        success.Should().BeTrue();
        message.Should().Contain("SID history");
    }

    [Fact]
    public async Task SetSidHistoryAsync_Failure_ReturnsFalse()
    {
        _processRunner.RunAsync("powershell", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = -1, StandardError = "RSAT not installed" });

        var creds = new DomainCredentials { Domain = "corp.local", Username = "admin", Password = "pass" };
        var (success, message) = await _service.SetSidHistoryAsync(NewSid, OldSid, creds);

        success.Should().BeFalse();
        message.Should().Contain("Failed");
    }

    [Fact]
    public async Task SetSidHistoryAsync_InvalidCreds_ReturnsFalse()
    {
        var creds = new DomainCredentials(); // IsValid = false

        var (success, message) = await _service.SetSidHistoryAsync(NewSid, OldSid, creds);

        success.Should().BeFalse();
        message.Should().Contain("credentials are required");
    }

    #endregion
}
