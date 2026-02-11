using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;
using NSubstitute;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Migration;

namespace ZeroInstall.Core.Tests.Migration;

public class UserAccountServiceTests
{
    private readonly IProcessRunner _processRunner = Substitute.For<IProcessRunner>();
    private readonly IRegistryAccessor _registry = Substitute.For<IRegistryAccessor>();
    private readonly UserAccountService _service;

    public UserAccountServiceTests()
    {
        _service = new UserAccountService(
            _processRunner, _registry, NullLogger<UserAccountService>.Instance);
    }

    #region UserExistsAsync

    [Fact]
    public async Task UserExistsAsync_ReturnsTrue_WhenNetUserSucceeds()
    {
        _processRunner.RunAsync("net", Arg.Is<string>(s => s.Contains("TestUser")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0, StandardOutput = "User name TestUser" });

        var result = await _service.UserExistsAsync("TestUser");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task UserExistsAsync_ReturnsFalse_WhenNetUserFails()
    {
        _processRunner.RunAsync("net", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1, StandardError = "The user name could not be found." });

        var result = await _service.UserExistsAsync("NonExistent");

        result.Should().BeFalse();
    }

    #endregion

    #region CreateUserAsync

    [Fact]
    public async Task CreateUserAsync_CallsNetUserAdd()
    {
        _processRunner.RunAsync("net", Arg.Is<string>(s => s.Contains("/add")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        _processRunner.RunAsync("powershell", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0, StandardOutput = "S-1-5-21-1234-5678-9012-1001\r\n" });

        await _service.CreateUserAsync("NewUser", "P@ssw0rd");

        await _processRunner.Received(1).RunAsync("net",
            Arg.Is<string>(s => s.Contains("\"NewUser\"") && s.Contains("/add")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateUserAsync_AddsToAdministrators_WhenIsAdminTrue()
    {
        _processRunner.RunAsync("net", Arg.Is<string>(s => s.Contains("/add")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        _processRunner.RunAsync("net", Arg.Is<string>(s => s.Contains("Administrators")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        _processRunner.RunAsync("powershell", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0, StandardOutput = "S-1-5-21-1234-5678-9012-1001\r\n" });

        await _service.CreateUserAsync("AdminUser", "P@ssw0rd", isAdmin: true);

        await _processRunner.Received(1).RunAsync("net",
            Arg.Is<string>(s => s.Contains("Administrators") && s.Contains("\"AdminUser\"")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateUserAsync_DoesNotAddToAdmins_WhenIsAdminFalse()
    {
        _processRunner.RunAsync("net", Arg.Is<string>(s => s.Contains("/add")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        _processRunner.RunAsync("powershell", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0, StandardOutput = "S-1-5-21-1234-5678-9012-1001\r\n" });

        await _service.CreateUserAsync("RegularUser", "P@ssw0rd", isAdmin: false);

        await _processRunner.DidNotReceive().RunAsync("net",
            Arg.Is<string>(s => s.Contains("Administrators")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateUserAsync_ThrowsOnFailure()
    {
        _processRunner.RunAsync("net", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1, StandardError = "Access denied" });

        var act = () => _service.CreateUserAsync("FailUser", "P@ssw0rd");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*FailUser*");
    }

    [Fact]
    public async Task CreateUserAsync_ReturnsSid()
    {
        _processRunner.RunAsync("net", Arg.Is<string>(s => s.Contains("/add")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        _processRunner.RunAsync("powershell", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0, StandardOutput = "S-1-5-21-1234-5678-9012-1001\r\n" });

        var sid = await _service.CreateUserAsync("NewUser", "P@ssw0rd");

        sid.Should().Be("S-1-5-21-1234-5678-9012-1001");
    }

    #endregion

    #region GetUserSidAsync

    [Fact]
    public async Task GetUserSidAsync_ReturnsSid_WhenPowerShellSucceeds()
    {
        _processRunner.RunAsync("powershell", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = "S-1-5-21-3623811015-3361044348-30300820-1013\r\n"
            });

        var sid = await _service.GetUserSidAsync("TestUser");

        sid.Should().Be("S-1-5-21-3623811015-3361044348-30300820-1013");
    }

    [Fact]
    public async Task GetUserSidAsync_ReturnsNull_WhenPowerShellFails()
    {
        _processRunner.RunAsync("powershell", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1, StandardError = "Exception" });

        var sid = await _service.GetUserSidAsync("UnknownUser");

        sid.Should().BeNull();
    }

    #endregion

    #region GetUserProfilePathAsync

    [Fact]
    public async Task GetUserProfilePathAsync_ReturnsRegistryPath_WhenSidFound()
    {
        _processRunner.RunAsync("powershell", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0, StandardOutput = "S-1-5-21-123-456-789-1001\r\n" });

        _registry.GetStringValue(
            RegistryHive.LocalMachine, RegistryView.Registry64,
            Arg.Is<string>(s => s.Contains("S-1-5-21-123-456-789-1001")), "ProfileImagePath")
            .Returns(@"C:\Users\TestUser");

        var path = await _service.GetUserProfilePathAsync("TestUser");

        path.Should().Be(@"C:\Users\TestUser");
    }

    [Fact]
    public async Task GetUserProfilePathAsync_ReturnsFallback_WhenRegistryEmpty()
    {
        _processRunner.RunAsync("powershell", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0, StandardOutput = "S-1-5-21-123-456-789-1001\r\n" });

        _registry.GetStringValue(Arg.Any<RegistryHive>(), Arg.Any<RegistryView>(),
            Arg.Any<string>(), "ProfileImagePath")
            .Returns((string?)null);

        var path = await _service.GetUserProfilePathAsync("FallbackUser");

        path.Should().Be(@"C:\Users\FallbackUser");
    }

    [Fact]
    public async Task GetUserProfilePathAsync_ReturnsFallback_WhenSidLookupFails()
    {
        _processRunner.RunAsync("powershell", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 1 });

        var path = await _service.GetUserProfilePathAsync("NoSidUser");

        path.Should().Be(@"C:\Users\NoSidUser");
    }

    #endregion

    #region ListLocalUsersAsync

    [Fact]
    public async Task ListLocalUsersAsync_ReturnsDiscoveredProfiles()
    {
        _registry.GetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry64,
            Arg.Is<string>(s => s.Contains("ProfileList")))
            .Returns(new[]
            {
                "S-1-5-18",
                "S-1-5-21-1234-5678-9012-1001",
                "S-1-5-21-1234-5678-9012-1002"
            });

        _registry.GetStringValue(RegistryHive.LocalMachine, RegistryView.Registry64,
            Arg.Is<string>(s => s.Contains("1001")), "ProfileImagePath")
            .Returns(@"C:\Users\Alice");

        _registry.GetStringValue(RegistryHive.LocalMachine, RegistryView.Registry64,
            Arg.Is<string>(s => s.Contains("1002")), "ProfileImagePath")
            .Returns(@"C:\Users\Bob");

        var users = await _service.ListLocalUsersAsync();

        users.Should().HaveCount(2);
        users[0].Username.Should().Be("Alice");
        users[0].Sid.Should().Be("S-1-5-21-1234-5678-9012-1001");
        users[0].ProfilePath.Should().Be(@"C:\Users\Alice");
        users[1].Username.Should().Be("Bob");
    }

    [Fact]
    public async Task ListLocalUsersAsync_SkipsSystemSids()
    {
        _registry.GetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry64,
            Arg.Any<string>())
            .Returns(new[]
            {
                "S-1-5-18",     // Local System
                "S-1-5-19",     // Local Service
                "S-1-5-20"      // Network Service
            });

        var users = await _service.ListLocalUsersAsync();

        users.Should().BeEmpty();
    }

    [Fact]
    public async Task ListLocalUsersAsync_SkipsEntriesWithNoProfilePath()
    {
        _registry.GetSubKeyNames(RegistryHive.LocalMachine, RegistryView.Registry64,
            Arg.Any<string>())
            .Returns(new[] { "S-1-5-21-1234-5678-9012-1001" });

        _registry.GetStringValue(Arg.Any<RegistryHive>(), Arg.Any<RegistryView>(),
            Arg.Any<string>(), "ProfileImagePath")
            .Returns((string?)null);

        var users = await _service.ListLocalUsersAsync();

        users.Should().BeEmpty();
    }

    #endregion

    #region DeleteUserAsync

    [Fact]
    public async Task DeleteUserAsync_Success_ReturnsTrue()
    {
        _processRunner.RunAsync("net", Arg.Is<string>(s => s.Contains("/delete")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        var result = await _service.DeleteUserAsync("OldUser");

        result.Should().BeTrue();
        await _processRunner.Received(1).RunAsync("net",
            Arg.Is<string>(s => s.Contains("\"OldUser\"") && s.Contains("/delete")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteUserAsync_Failure_ReturnsFalse()
    {
        _processRunner.RunAsync("net", Arg.Is<string>(s => s.Contains("/delete")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = -1, StandardError = "User not found" });

        var result = await _service.DeleteUserAsync("NotFound");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteUserAsync_NotFound_ReturnsFalse()
    {
        _processRunner.RunAsync("net", Arg.Is<string>(s => s.Contains("/delete")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 2, StandardError = "The user name could not be found." });

        var result = await _service.DeleteUserAsync("Ghost");

        result.Should().BeFalse();
    }

    #endregion

    #region DisableUserAsync

    [Fact]
    public async Task DisableUserAsync_Success_ReturnsTrue()
    {
        _processRunner.RunAsync("net", Arg.Is<string>(s => s.Contains("/active:no")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        var result = await _service.DisableUserAsync("OldUser");

        result.Should().BeTrue();
        await _processRunner.Received(1).RunAsync("net",
            Arg.Is<string>(s => s.Contains("\"OldUser\"") && s.Contains("/active:no")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DisableUserAsync_Failure_ReturnsFalse()
    {
        _processRunner.RunAsync("net", Arg.Is<string>(s => s.Contains("/active:no")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = -1, StandardError = "Access denied" });

        var result = await _service.DisableUserAsync("Admin");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task DisableUserAsync_NotFound_ReturnsFalse()
    {
        _processRunner.RunAsync("net", Arg.Is<string>(s => s.Contains("/active:no")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 2, StandardError = "The user name could not be found." });

        var result = await _service.DisableUserAsync("Ghost");

        result.Should().BeFalse();
    }

    #endregion

    #region SetAutoLogonAsync

    [Fact]
    public async Task SetAutoLogonAsync_SetsRegistryValues()
    {
        var result = await _service.SetAutoLogonAsync("Bill", "password123");

        result.Should().BeTrue();
        _registry.Received().SetStringValue(
            RegistryHive.LocalMachine, RegistryView.Registry64,
            Arg.Any<string>(), "AutoAdminLogon", "1");
        _registry.Received().SetStringValue(
            RegistryHive.LocalMachine, RegistryView.Registry64,
            Arg.Any<string>(), "DefaultUserName", "Bill");
        _registry.Received().SetStringValue(
            RegistryHive.LocalMachine, RegistryView.Registry64,
            Arg.Any<string>(), "DefaultPassword", "password123");
    }

    [Fact]
    public async Task SetAutoLogonAsync_ClearsOnEmptyPassword()
    {
        var result = await _service.SetAutoLogonAsync("Bill", null);

        result.Should().BeTrue();
        _registry.Received().SetStringValue(
            RegistryHive.LocalMachine, RegistryView.Registry64,
            Arg.Any<string>(), "AutoAdminLogon", "0");
        _registry.Received().SetStringValue(
            RegistryHive.LocalMachine, RegistryView.Registry64,
            Arg.Any<string>(), "DefaultUserName", "");
        _registry.Received().SetStringValue(
            RegistryHive.LocalMachine, RegistryView.Registry64,
            Arg.Any<string>(), "DefaultPassword", "");
    }

    #endregion

    #region ExistingTests_StillPass

    [Fact]
    public async Task ExistingCreateUser_StillWorks()
    {
        _processRunner.RunAsync("net", Arg.Is<string>(s => s.Contains("/add")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        _processRunner.RunAsync("powershell", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0, StandardOutput = "S-1-5-21-1234-5678-9012-1001\r\n" });

        var sid = await _service.CreateUserAsync("TestUser", "P@ss");

        sid.Should().StartWith("S-1-5-21-");
    }

    [Fact]
    public async Task ExistingUserExists_StillWorks()
    {
        _processRunner.RunAsync("net", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0, StandardOutput = "User name TestUser" });

        var exists = await _service.UserExistsAsync("TestUser");

        exists.Should().BeTrue();
    }

    #endregion

    #region ParseSidFromOutput

    [Fact]
    public void ParseSidFromOutput_ExtractsValidSid()
    {
        var output = "S-1-5-21-3623811015-3361044348-30300820-1013\r\n";

        UserAccountService.ParseSidFromOutput(output)
            .Should().Be("S-1-5-21-3623811015-3361044348-30300820-1013");
    }

    [Fact]
    public void ParseSidFromOutput_ReturnsNull_ForInvalidOutput()
    {
        UserAccountService.ParseSidFromOutput("Some error text")
            .Should().BeNull();
    }

    [Fact]
    public void ParseSidFromOutput_ReturnsNull_ForEmptyOutput()
    {
        UserAccountService.ParseSidFromOutput("  ")
            .Should().BeNull();
    }

    [Fact]
    public void ParseSidFromOutput_HandlesMultilineOutput()
    {
        var output = "S-1-5-21-1234-5678-9012-1001\r\nExtra line\r\n";

        UserAccountService.ParseSidFromOutput(output)
            .Should().Be("S-1-5-21-1234-5678-9012-1001");
    }

    #endregion
}
