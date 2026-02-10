using Microsoft.Extensions.Logging.Abstractions;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Migration;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;
using ZeroInstall.WinPE.Services;

namespace ZeroInstall.WinPE.Tests.Services;

public class RestoreOrchestratorTests
{
    private readonly IDiskCloner _diskCloner = Substitute.For<IDiskCloner>();
    private readonly DriverInjectionService _driverInjection;
    private readonly RestoreOrchestrator _orchestrator;

    public RestoreOrchestratorTests()
    {
        var processRunner = Substitute.For<IProcessRunner>();
        _driverInjection = new DriverInjectionService(processRunner, NullLogger<DriverInjectionService>.Instance);
        _orchestrator = new RestoreOrchestrator(_diskCloner, _driverInjection, NullLogger<RestoreOrchestrator>.Instance);
    }

    [Fact]
    public async Task RunRestoreAsync_VerifyCalled_WhenNotSkipped()
    {
        _diskCloner.VerifyImageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var options = new RestoreOptions { SkipVerify = false };
        await _orchestrator.RunRestoreAsync("test.img", @"D:\", options);

        await _diskCloner.Received(1).VerifyImageAsync("test.img", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunRestoreAsync_VerifySkipped_WhenOptionSet()
    {
        var options = new RestoreOptions { SkipVerify = true };
        await _orchestrator.RunRestoreAsync("test.img", @"D:\", options);

        await _diskCloner.DidNotReceive().VerifyImageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunRestoreAsync_RestoreCalled()
    {
        _diskCloner.VerifyImageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var options = new RestoreOptions { SkipVerify = false };
        await _orchestrator.RunRestoreAsync("test.img", @"D:\", options);

        await _diskCloner.Received(1).RestoreImageAsync(
            "test.img", @"D:\", Arg.Any<IProgress<TransferProgress>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunRestoreAsync_DriverInjectionSkipped_WhenNoDriverPath()
    {
        _diskCloner.VerifyImageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var options = new RestoreOptions { SkipVerify = false, DriverPath = null };
        var result = await _orchestrator.RunRestoreAsync("test.img", @"D:\", options);

        result.Success.Should().BeTrue();
        result.DriverResult.Should().BeNull();
    }

    [Fact]
    public async Task RunRestoreAsync_VerificationFailed_ReturnsError()
    {
        _diskCloner.VerifyImageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var options = new RestoreOptions { SkipVerify = false };
        var result = await _orchestrator.RunRestoreAsync("test.img", @"D:\", options);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("integrity");
        await _diskCloner.DidNotReceive().RestoreImageAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IProgress<TransferProgress>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunRestoreAsync_RestoreThrows_ReturnsError()
    {
        _diskCloner.VerifyImageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _diskCloner.RestoreImageAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IProgress<TransferProgress>>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new IOException("Disk full"));

        var options = new RestoreOptions { SkipVerify = false };
        var result = await _orchestrator.RunRestoreAsync("test.img", @"D:\", options);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Disk full");
    }
}
