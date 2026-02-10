using Microsoft.Extensions.Logging.Abstractions;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Migration;

namespace ZeroInstall.WinPE.Tests.Migration;

public class DriverInjectionServiceTests
{
    private readonly IProcessRunner _processRunner = Substitute.For<IProcessRunner>();
    private readonly DriverInjectionService _service;

    public DriverInjectionServiceTests()
    {
        _service = new DriverInjectionService(_processRunner, NullLogger<DriverInjectionService>.Instance);
    }

    [Fact]
    public async Task InjectDriversAsync_CallsDismWithCorrectArgs()
    {
        _processRunner.RunAsync("DISM.exe", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = "The operation completed successfully."
            });

        await _service.InjectDriversAsync(@"D:\", @"E:\Drivers", recurse: false);

        await _processRunner.Received(1).RunAsync("DISM.exe",
            Arg.Is<string>(s => s.Contains("/Image:\"D:\\\"") && s.Contains("/Driver:\"E:\\Drivers\"") && !s.Contains("/Recurse")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InjectDriversAsync_WithRecurse_IncludesRecurseFlag()
    {
        _processRunner.RunAsync("DISM.exe", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = "The operation completed successfully."
            });

        await _service.InjectDriversAsync(@"D:\", @"E:\Drivers", recurse: true);

        await _processRunner.Received(1).RunAsync("DISM.exe",
            Arg.Is<string>(s => s.Contains("/Recurse")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InjectDriversAsync_SuccessOutput_ReturnsSuccessResult()
    {
        _processRunner.RunAsync("DISM.exe", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = "The operation completed successfully."
            });

        var result = await _service.InjectDriversAsync(@"D:\", @"E:\Drivers");

        result.Success.Should().BeTrue();
        result.AddedCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task InjectDriversAsync_FailedProcess_ReturnsErrorResult()
    {
        _processRunner.RunAsync("DISM.exe", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult
            {
                ExitCode = 1,
                StandardError = "Access is denied."
            });

        var result = await _service.InjectDriversAsync(@"D:\", @"E:\Drivers");

        result.Errors.Should().NotBeEmpty();
    }

    // --- ParseDismOutput Tests ---

    [Fact]
    public void ParseDismOutput_SuccessWithCounts_ParsesCorrectly()
    {
        var output = """
            Installing 3 of 5 - oem1.inf
            Installing 4 of 5 - oem2.inf
            3 of 5 drivers installed successfully.
            """;

        var result = DriverInjectionService.ParseDismOutput(output);

        result.AddedCount.Should().Be(3);
        result.FailedCount.Should().Be(2);
    }

    [Fact]
    public void ParseDismOutput_OperationCompletedSuccessfully_ReturnsSuccess()
    {
        var output = "The operation completed successfully.";

        var result = DriverInjectionService.ParseDismOutput(output);

        result.Success.Should().BeTrue();
        result.AddedCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ParseDismOutput_EmptyString_ReturnsDefaultResult()
    {
        var result = DriverInjectionService.ParseDismOutput("");

        result.AddedCount.Should().Be(0);
        result.FailedCount.Should().Be(0);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ParseDismOutput_ErrorLines_CapturedInErrors()
    {
        var output = "Error: The driver package could not be installed.\r\nSome other line";

        var result = DriverInjectionService.ParseDismOutput(output);

        result.Errors.Should().Contain(e => e.Contains("Error:"));
    }

    // --- GetDriverFiles Tests ---

    [Fact]
    public async Task GetDriverFilesAsync_FoundFiles_ReturnsInfPaths()
    {
        _processRunner.RunAsync("powershell", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = "C:\\Drivers\\net\\e1000.inf\r\nC:\\Drivers\\audio\\hda.inf\r\n"
            });

        var result = await _service.GetDriverFilesAsync(@"C:\Drivers");

        result.Should().HaveCount(2);
        result[0].Should().Be(@"C:\Drivers\net\e1000.inf");
        result[1].Should().Be(@"C:\Drivers\audio\hda.inf");
    }

    [Fact]
    public async Task GetDriverFilesAsync_NoFiles_ReturnsEmpty()
    {
        _processRunner.RunAsync("powershell", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult
            {
                ExitCode = 1,
                StandardOutput = ""
            });

        var result = await _service.GetDriverFilesAsync(@"C:\Empty");

        result.Should().BeEmpty();
    }
}
