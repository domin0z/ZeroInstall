using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;

namespace ZeroInstall.Core.Tests.Services;

public class BitLockerServiceTests
{
    private readonly IProcessRunner _processRunner = Substitute.For<IProcessRunner>();
    private readonly BitLockerService _service;

    public BitLockerServiceTests()
    {
        _service = new BitLockerService(_processRunner, NullLogger<BitLockerService>.Instance);
    }

    #region NormalizeVolumePath

    [Theory]
    [InlineData("C:", "C:")]
    [InlineData("c:", "C:")]
    [InlineData(@"C:\", "C:")]
    [InlineData("C", "C:")]
    [InlineData("d", "D:")]
    [InlineData(@"D:\", "D:")]
    public void NormalizeVolumePath_NormalizesCorrectly(string input, string expected)
    {
        BitLockerService.NormalizeVolumePath(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void NormalizeVolumePath_EmptyOrNull_ReturnsEmpty(string? input)
    {
        BitLockerService.NormalizeVolumePath(input!).Should().BeEmpty();
    }

    #endregion

    #region ParseBitLockerStatus — Protection On + Unlocked

    [Fact]
    public void ParseBitLockerStatus_ProtectionOn_Unlocked()
    {
        var output = """
            BitLocker Drive Encryption: Configuration Tool version 10.0.19041
            Volume C: [Windows]
            [OS Volume]

                Size:                 237.92 GB
                BitLocker Version:    2.0
                Conversion Status:    Fully Encrypted
                Percentage Encrypted: 100.0%
                Encryption Method:    XTS-AES 128
                Protection Status:    Protection On
                Lock Status:          Unlocked
                Identification Field: Unknown
                Key Protectors:
                    TPM
                    Numerical Password
            """;

        var status = BitLockerService.ParseBitLockerStatus("C:", output);

        status.VolumePath.Should().Be("C:");
        status.ProtectionStatus.Should().Be(BitLockerProtectionStatus.Unlocked);
        status.IsEncrypted.Should().BeTrue();
        status.LockStatus.Should().Be("Unlocked");
        status.EncryptionMethod.Should().Be("XTS-AES 128");
        status.ConversionStatus.Should().Be("Fully Encrypted");
        status.PercentageEncrypted.Should().Be(100.0);
        status.KeyProtectors.Should().Contain("TPM");
        status.KeyProtectors.Should().Contain("Numerical Password");
        status.KeyProtectors.Should().HaveCount(2);
    }

    #endregion

    #region ParseBitLockerStatus — Protection On + Locked

    [Fact]
    public void ParseBitLockerStatus_ProtectionOn_Locked()
    {
        var output = """
            Volume D: [Data]

                Size:                 500.00 GB
                BitLocker Version:    2.0
                Conversion Status:    Fully Encrypted
                Percentage Encrypted: 100.0%
                Encryption Method:    AES-CBC 256
                Protection Status:    Protection On
                Lock Status:          Locked
                Identification Field: Unknown
                Key Protectors:
                    Recovery Password
            """;

        var status = BitLockerService.ParseBitLockerStatus("D:", output);

        status.ProtectionStatus.Should().Be(BitLockerProtectionStatus.Locked);
        status.IsEncrypted.Should().BeTrue();
        status.LockStatus.Should().Be("Locked");
        status.EncryptionMethod.Should().Be("AES-CBC 256");
        status.KeyProtectors.Should().Contain("Recovery Password");
        status.KeyProtectors.Should().HaveCount(1);
    }

    #endregion

    #region ParseBitLockerStatus — Protection Off (Suspended)

    [Fact]
    public void ParseBitLockerStatus_ProtectionOff_Suspended()
    {
        var output = """
            Volume C: [Windows]

                Size:                 237.92 GB
                BitLocker Version:    2.0
                Conversion Status:    Fully Encrypted
                Percentage Encrypted: 100.0%
                Encryption Method:    XTS-AES 128
                Protection Status:    Protection Off
                Lock Status:          Unlocked
                Key Protectors:
                    TPM
            """;

        var status = BitLockerService.ParseBitLockerStatus("C:", output);

        status.ProtectionStatus.Should().Be(BitLockerProtectionStatus.Suspended);
        status.IsEncrypted.Should().BeTrue();
    }

    #endregion

    #region ParseBitLockerStatus — Not Protected (Fully Decrypted)

    [Fact]
    public void ParseBitLockerStatus_FullyDecrypted_NotProtected()
    {
        var output = """
            Volume C: [Windows]

                Size:                 237.92 GB
                BitLocker Version:    None
                Conversion Status:    Fully Decrypted
                Percentage Encrypted: 0.0%
                Encryption Method:    None
                Protection Status:    Protection Off
                Lock Status:          Unlocked
                Key Protectors:       None Found
            """;

        var status = BitLockerService.ParseBitLockerStatus("C:", output);

        status.ProtectionStatus.Should().Be(BitLockerProtectionStatus.NotProtected);
        status.IsEncrypted.Should().BeFalse();
        status.PercentageEncrypted.Should().Be(0.0);
        status.EncryptionMethod.Should().Be("None");
    }

    #endregion

    #region ParseBitLockerStatus — Empty / Invalid Output

    [Fact]
    public void ParseBitLockerStatus_EmptyOutput_ReturnsUnknown()
    {
        var status = BitLockerService.ParseBitLockerStatus("C:", "");

        status.ProtectionStatus.Should().Be(BitLockerProtectionStatus.Unknown);
        status.IsEncrypted.Should().BeFalse();
    }

    [Fact]
    public void ParseBitLockerStatus_InvalidOutput_ReturnsUnknown()
    {
        var status = BitLockerService.ParseBitLockerStatus("C:", "This is not manage-bde output");

        status.ProtectionStatus.Should().Be(BitLockerProtectionStatus.Unknown);
    }

    #endregion

    #region ParseBitLockerStatus — Percentage Parsing

    [Theory]
    [InlineData("100.0%", 100.0)]
    [InlineData("50.5%", 50.5)]
    [InlineData("0%", 0.0)]
    [InlineData("99.9%", 99.9)]
    public void ParseBitLockerStatus_ParsesPercentage(string pctValue, double expected)
    {
        var output = $"""
            Volume C:
                Conversion Status:    Fully Encrypted
                Percentage Encrypted: {pctValue}
                Encryption Method:    XTS-AES 128
                Protection Status:    Protection On
                Lock Status:          Unlocked
            """;

        var status = BitLockerService.ParseBitLockerStatus("C:", output);

        status.PercentageEncrypted.Should().Be(expected);
    }

    #endregion

    #region ParseBitLockerStatus — Multiple Key Protectors

    [Fact]
    public void ParseBitLockerStatus_MultipleKeyProtectors()
    {
        var output = """
            Volume C:
                Conversion Status:    Fully Encrypted
                Percentage Encrypted: 100.0%
                Encryption Method:    XTS-AES 256
                Protection Status:    Protection On
                Lock Status:          Unlocked
                Key Protectors:
                    TPM
                    Numerical Password
                    External Key
            """;

        var status = BitLockerService.ParseBitLockerStatus("C:", output);

        status.KeyProtectors.Should().HaveCount(3);
        status.KeyProtectors.Should().Contain("TPM");
        status.KeyProtectors.Should().Contain("Numerical Password");
        status.KeyProtectors.Should().Contain("External Key");
    }

    #endregion

    #region GetStatusAsync

    [Fact]
    public async Task GetStatusAsync_NotEncrypted_ReturnsNotProtected()
    {
        _processRunner.RunAsync("manage-bde", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = """
                    Volume C:
                        Conversion Status:    Fully Decrypted
                        Percentage Encrypted: 0.0%
                        Encryption Method:    None
                        Protection Status:    Protection Off
                        Lock Status:          Unlocked
                    """
            });

        var status = await _service.GetStatusAsync("C:");

        status.ProtectionStatus.Should().Be(BitLockerProtectionStatus.NotProtected);
        status.IsEncrypted.Should().BeFalse();
    }

    [Fact]
    public async Task GetStatusAsync_Encrypted_ReturnsUnlocked()
    {
        _processRunner.RunAsync("manage-bde", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = """
                    Volume C:
                        Conversion Status:    Fully Encrypted
                        Percentage Encrypted: 100.0%
                        Encryption Method:    XTS-AES 128
                        Protection Status:    Protection On
                        Lock Status:          Unlocked
                        Key Protectors:
                            TPM
                    """
            });

        var status = await _service.GetStatusAsync("C:");

        status.ProtectionStatus.Should().Be(BitLockerProtectionStatus.Unlocked);
        status.IsEncrypted.Should().BeTrue();
    }

    [Fact]
    public async Task GetStatusAsync_CommandFailure_ReturnsUnknown()
    {
        _processRunner.RunAsync("manage-bde", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult
            {
                ExitCode = -1,
                StandardError = "manage-bde is not recognized"
            });

        var status = await _service.GetStatusAsync("C:");

        status.ProtectionStatus.Should().Be(BitLockerProtectionStatus.Unknown);
    }

    [Fact]
    public async Task GetStatusAsync_Exception_ReturnsUnknown()
    {
        _processRunner.RunAsync("manage-bde", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<ProcessResult>(_ => throw new InvalidOperationException("process failed"));

        var status = await _service.GetStatusAsync("C:");

        status.ProtectionStatus.Should().Be(BitLockerProtectionStatus.Unknown);
    }

    [Fact]
    public async Task GetStatusAsync_NormalizesVolumePath()
    {
        _processRunner.RunAsync("manage-bde", Arg.Is<string>(s => s.Contains("C:")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = """
                    Volume C:
                        Conversion Status:    Fully Decrypted
                        Protection Status:    Protection Off
                        Lock Status:          Unlocked
                    """
            });

        await _service.GetStatusAsync(@"C:\");

        await _processRunner.Received(1).RunAsync("manage-bde",
            Arg.Is<string>(s => s.Contains("C:")),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region UnlockVolumeAsync

    [Fact]
    public async Task UnlockVolumeAsync_RecoveryPassword_Success()
    {
        _processRunner.RunAsync("manage-bde", Arg.Is<string>(s => s.Contains("-RecoveryPassword")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        var result = await _service.UnlockVolumeAsync("D:", recoveryPassword: "123456-789012-345678-901234-567890-123456-789012-345678");

        result.Should().BeTrue();
        await _processRunner.Received(1).RunAsync("manage-bde",
            Arg.Is<string>(s => s.Contains("-unlock") && s.Contains("-RecoveryPassword") && s.Contains("123456")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnlockVolumeAsync_RecoveryKeyFile_Success()
    {
        _processRunner.RunAsync("manage-bde", Arg.Is<string>(s => s.Contains("-RecoveryKey")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        var result = await _service.UnlockVolumeAsync("D:", recoveryKeyPath: @"E:\keys\recovery.bek");

        result.Should().BeTrue();
        await _processRunner.Received(1).RunAsync("manage-bde",
            Arg.Is<string>(s => s.Contains("-RecoveryKey") && s.Contains("recovery.bek")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnlockVolumeAsync_Failure_ReturnsFalse()
    {
        _processRunner.RunAsync("manage-bde", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = -1, StandardError = "The password is incorrect" });

        var result = await _service.UnlockVolumeAsync("D:", recoveryPassword: "wrong-password");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task UnlockVolumeAsync_NoCredentials_ReturnsFalse()
    {
        var result = await _service.UnlockVolumeAsync("D:");

        result.Should().BeFalse();
        await _processRunner.DidNotReceive().RunAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnlockVolumeAsync_Exception_ReturnsFalse()
    {
        _processRunner.RunAsync("manage-bde", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<ProcessResult>(_ => throw new InvalidOperationException("process error"));

        var result = await _service.UnlockVolumeAsync("D:", recoveryPassword: "123456-789012");

        result.Should().BeFalse();
    }

    #endregion

    #region SuspendProtectionAsync

    [Fact]
    public async Task SuspendProtectionAsync_Indefinite_Success()
    {
        _processRunner.RunAsync("manage-bde", Arg.Is<string>(s => s.Contains("-disable") && !s.Contains("-RebootCount")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        var result = await _service.SuspendProtectionAsync("C:");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task SuspendProtectionAsync_WithRebootCount_Success()
    {
        _processRunner.RunAsync("manage-bde", Arg.Is<string>(s => s.Contains("-RebootCount 3")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        var result = await _service.SuspendProtectionAsync("C:", rebootCount: 3);

        result.Should().BeTrue();
        await _processRunner.Received(1).RunAsync("manage-bde",
            Arg.Is<string>(s => s.Contains("-RebootCount 3")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SuspendProtectionAsync_Failure_ReturnsFalse()
    {
        _processRunner.RunAsync("manage-bde", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = -1, StandardError = "Access denied" });

        var result = await _service.SuspendProtectionAsync("C:");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task SuspendProtectionAsync_Exception_ReturnsFalse()
    {
        _processRunner.RunAsync("manage-bde", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<ProcessResult>(_ => throw new InvalidOperationException("error"));

        var result = await _service.SuspendProtectionAsync("C:");

        result.Should().BeFalse();
    }

    #endregion

    #region ResumeProtectionAsync

    [Fact]
    public async Task ResumeProtectionAsync_Success()
    {
        _processRunner.RunAsync("manage-bde", Arg.Is<string>(s => s.Contains("-enable")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0 });

        var result = await _service.ResumeProtectionAsync("C:");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ResumeProtectionAsync_Failure_ReturnsFalse()
    {
        _processRunner.RunAsync("manage-bde", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = -1, StandardError = "Not suspended" });

        var result = await _service.ResumeProtectionAsync("C:");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ResumeProtectionAsync_Exception_ReturnsFalse()
    {
        _processRunner.RunAsync("manage-bde", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<ProcessResult>(_ => throw new InvalidOperationException("error"));

        var result = await _service.ResumeProtectionAsync("C:");

        result.Should().BeFalse();
    }

    #endregion

    #region BitLockerStatus Model

    [Fact]
    public void BitLockerStatus_IsEncrypted_TrueForUnlocked()
    {
        var status = new BitLockerStatus { ProtectionStatus = BitLockerProtectionStatus.Unlocked };
        status.IsEncrypted.Should().BeTrue();
    }

    [Fact]
    public void BitLockerStatus_IsEncrypted_TrueForLocked()
    {
        var status = new BitLockerStatus { ProtectionStatus = BitLockerProtectionStatus.Locked };
        status.IsEncrypted.Should().BeTrue();
    }

    [Fact]
    public void BitLockerStatus_IsEncrypted_TrueForSuspended()
    {
        var status = new BitLockerStatus { ProtectionStatus = BitLockerProtectionStatus.Suspended };
        status.IsEncrypted.Should().BeTrue();
    }

    [Fact]
    public void BitLockerStatus_IsEncrypted_FalseForNotProtected()
    {
        var status = new BitLockerStatus { ProtectionStatus = BitLockerProtectionStatus.NotProtected };
        status.IsEncrypted.Should().BeFalse();
    }

    [Fact]
    public void BitLockerStatus_IsEncrypted_FalseForUnknown()
    {
        var status = new BitLockerStatus { ProtectionStatus = BitLockerProtectionStatus.Unknown };
        status.IsEncrypted.Should().BeFalse();
    }

    [Fact]
    public void BitLockerStatus_DefaultValues()
    {
        var status = new BitLockerStatus();

        status.VolumePath.Should().BeEmpty();
        status.ProtectionStatus.Should().Be(BitLockerProtectionStatus.Unknown);
        status.LockStatus.Should().BeEmpty();
        status.EncryptionMethod.Should().BeEmpty();
        status.ConversionStatus.Should().BeEmpty();
        status.PercentageEncrypted.Should().Be(0);
        status.KeyProtectors.Should().BeEmpty();
        status.RawOutput.Should().BeEmpty();
    }

    #endregion
}
