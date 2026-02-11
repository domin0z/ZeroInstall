using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;

namespace ZeroInstall.Core.Tests.Services;

public class DomainJoinServiceTests
{
    private readonly IProcessRunner _processRunner = Substitute.For<IProcessRunner>();
    private readonly DomainJoinService _service;

    public DomainJoinServiceTests()
    {
        _service = new DomainJoinService(_processRunner, NullLogger<DomainJoinService>.Instance);
    }

    private static DomainCredentials ValidCreds => new()
    {
        Domain = "corp.local",
        Username = "admin",
        Password = "P@ss123"
    };

    #region JoinDomainAsync

    [Fact]
    public async Task JoinDomainAsync_Success_ReturnsTrue()
    {
        _processRunner.RunAsync("powershell", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        var (success, message) = await _service.JoinDomainAsync("corp.local", null, ValidCreds);

        success.Should().BeTrue();
        message.Should().Contain("corp.local");
        message.Should().Contain("restart");
    }

    [Fact]
    public async Task JoinDomainAsync_Failure_ReturnsFalse()
    {
        _processRunner.RunAsync("powershell", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = -1, StandardError = "Access denied" });

        var (success, message) = await _service.JoinDomainAsync("corp.local", null, ValidCreds);

        success.Should().BeFalse();
        message.Should().Contain("Failed");
    }

    [Fact]
    public async Task JoinDomainAsync_WithOu_IncludesOuInCommand()
    {
        _processRunner.RunAsync("powershell", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        await _service.JoinDomainAsync("corp.local", "OU=PCs,DC=corp,DC=local", ValidCreds);

        await _processRunner.Received(1).RunAsync("powershell",
            Arg.Is<string>(s => s.Contains("OUPath") && s.Contains("OU=PCs")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinDomainAsync_WithRename_IncludesNewNameInCommand()
    {
        _processRunner.RunAsync("powershell", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        var (success, message) = await _service.JoinDomainAsync(
            "corp.local", null, ValidCreds, "NEWPC01");

        success.Should().BeTrue();
        message.Should().Contain("NEWPC01");
        await _processRunner.Received(1).RunAsync("powershell",
            Arg.Is<string>(s => s.Contains("NewName") && s.Contains("NEWPC01")),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region UnjoinDomainAsync

    [Fact]
    public async Task UnjoinDomainAsync_Success_ReturnsTrue()
    {
        _processRunner.RunAsync("powershell", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        var (success, message) = await _service.UnjoinDomainAsync();

        success.Should().BeTrue();
        message.Should().Contain("WORKGROUP");
    }

    [Fact]
    public async Task UnjoinDomainAsync_Failure_ReturnsFalse()
    {
        _processRunner.RunAsync("powershell", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = -1, StandardError = "Not joined" });

        var (success, message) = await _service.UnjoinDomainAsync();

        success.Should().BeFalse();
        message.Should().Contain("Failed");
    }

    #endregion

    #region JoinAzureAdAsync

    [Fact]
    public async Task JoinAzureAdAsync_Success_ReturnsTrue()
    {
        _processRunner.RunAsync("dsregcmd", "/join", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        var (success, message) = await _service.JoinAzureAdAsync();

        success.Should().BeTrue();
        message.Should().Contain("Azure AD");
    }

    #endregion

    #region RenameComputerAsync

    [Fact]
    public async Task RenameComputerAsync_Success_ReturnsTrue()
    {
        _processRunner.RunAsync("powershell", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        var (success, message) = await _service.RenameComputerAsync("NEWPC01");

        success.Should().BeTrue();
        message.Should().Contain("NEWPC01");
        message.Should().Contain("restart");
    }

    [Fact]
    public async Task RenameComputerAsync_Failure_ReturnsFalse()
    {
        _processRunner.RunAsync("powershell", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = -1, StandardError = "Access denied" });

        var (success, message) = await _service.RenameComputerAsync("NEWPC01");

        success.Should().BeFalse();
        message.Should().Contain("Failed");
    }

    [Fact]
    public async Task RenameComputerAsync_WithCreds_IncludesCredentialInCommand()
    {
        _processRunner.RunAsync("powershell", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        await _service.RenameComputerAsync("NEWPC01", ValidCreds);

        await _processRunner.Received(1).RunAsync("powershell",
            Arg.Is<string>(s => s.Contains("DomainCredential")),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region BuildCredentialPart

    [Fact]
    public void BuildCredentialPart_ContainsDomainAndUsername()
    {
        var result = DomainJoinService.BuildCredentialPart(ValidCreds);

        result.Should().Contain("corp.local\\admin");
        result.Should().Contain("PSCredential");
        result.Should().Contain("ConvertTo-SecureString");
    }

    #endregion
}
