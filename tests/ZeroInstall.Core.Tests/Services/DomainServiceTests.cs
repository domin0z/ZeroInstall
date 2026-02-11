using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ZeroInstall.Core.Discovery;
using ZeroInstall.Core.Enums;
using ZeroInstall.Core.Models;
using ZeroInstall.Core.Services;

namespace ZeroInstall.Core.Tests.Services;

public class DomainServiceTests
{
    private readonly IProcessRunner _processRunner = Substitute.For<IProcessRunner>();
    private readonly DomainService _service;

    public DomainServiceTests()
    {
        _service = new DomainService(_processRunner, NullLogger<DomainService>.Instance);
    }

    #region Model Defaults

    [Fact]
    public void DomainInfo_DefaultValues()
    {
        var info = new DomainInfo();

        info.JoinType.Should().Be(DomainJoinType.Unknown);
        info.DomainOrWorkgroup.Should().BeEmpty();
        info.IsDomainJoined.Should().BeFalse();
        info.AzureAdTenantName.Should().BeNull();
        info.AzureAdTenantId.Should().BeNull();
        info.AzureAdDeviceId.Should().BeNull();
        info.DomainController.Should().BeNull();
        info.RawOutput.Should().BeEmpty();
    }

    [Fact]
    public void DomainInfo_IsDomainJoined_TrueForAd()
    {
        var info = new DomainInfo { JoinType = DomainJoinType.ActiveDirectory };
        info.IsDomainJoined.Should().BeTrue();
    }

    [Fact]
    public void DomainInfo_IsDomainJoined_TrueForHybrid()
    {
        var info = new DomainInfo { JoinType = DomainJoinType.HybridAzureAd };
        info.IsDomainJoined.Should().BeTrue();
    }

    [Fact]
    public void DomainCredentials_DefaultValues()
    {
        var creds = new DomainCredentials();

        creds.Domain.Should().BeEmpty();
        creds.Username.Should().BeEmpty();
        creds.Password.Should().BeEmpty();
        creds.IsValid.Should().BeFalse();
    }

    [Fact]
    public void DomainCredentials_IsValid_TrueWhenAllSet()
    {
        var creds = new DomainCredentials
        {
            Domain = "corp.local",
            Username = "admin",
            Password = "secret"
        };

        creds.IsValid.Should().BeTrue();
    }

    [Fact]
    public void DomainMigrationConfiguration_DefaultValues()
    {
        var config = new DomainMigrationConfiguration();

        config.TargetDomain.Should().BeNull();
        config.TargetOu.Should().BeNull();
        config.ComputerNewName.Should().BeNull();
        config.DomainCredentials.Should().NotBeNull();
        config.JoinAzureAd.Should().BeFalse();
        config.IncludeSidHistory.Should().BeFalse();
        config.UserLookupMap.Should().BeEmpty();
        config.PostMigrationScript.Should().BeNull();
        config.PostMigrationAccountAction.Should().Be(PostMigrationAccountAction.None);
    }

    [Fact]
    public void DomainMigrationConfiguration_SetProperties()
    {
        var config = new DomainMigrationConfiguration
        {
            TargetDomain = "corp.local",
            TargetOu = "OU=PCs,DC=corp,DC=local",
            ComputerNewName = "NEWPC01",
            JoinAzureAd = true,
            IncludeSidHistory = true,
            PostMigrationScript = @"C:\scripts\post.ps1",
            PostMigrationAccountAction = PostMigrationAccountAction.Disable,
            UserLookupMap = new Dictionary<string, string> { ["Bill"] = "William" }
        };

        config.TargetDomain.Should().Be("corp.local");
        config.TargetOu.Should().Be("OU=PCs,DC=corp,DC=local");
        config.ComputerNewName.Should().Be("NEWPC01");
        config.JoinAzureAd.Should().BeTrue();
        config.IncludeSidHistory.Should().BeTrue();
        config.PostMigrationScript.Should().Be(@"C:\scripts\post.ps1");
        config.PostMigrationAccountAction.Should().Be(PostMigrationAccountAction.Disable);
        config.UserLookupMap.Should().ContainKey("Bill");
    }

    [Fact]
    public void DomainMigrationConfiguration_UserLookupMap_WorksAsExpected()
    {
        var config = new DomainMigrationConfiguration
        {
            UserLookupMap = new Dictionary<string, string>
            {
                ["OldUser1"] = "NewUser1",
                ["OldUser2"] = "NewUser2"
            }
        };

        config.UserLookupMap.Should().HaveCount(2);
        config.UserLookupMap["OldUser1"].Should().Be("NewUser1");
    }

    #endregion

    #region Enum Existence

    [Fact]
    public void DomainJoinType_AllValuesExist()
    {
        DomainJoinType.Unknown.Should().BeDefined();
        DomainJoinType.Workgroup.Should().BeDefined();
        DomainJoinType.ActiveDirectory.Should().BeDefined();
        DomainJoinType.AzureAd.Should().BeDefined();
        DomainJoinType.HybridAzureAd.Should().BeDefined();
    }

    [Fact]
    public void UserAccountType_AllValuesExist()
    {
        UserAccountType.Unknown.Should().BeDefined();
        UserAccountType.Local.Should().BeDefined();
        UserAccountType.ActiveDirectory.Should().BeDefined();
        UserAccountType.AzureAd.Should().BeDefined();
        UserAccountType.MicrosoftAccount.Should().BeDefined();
    }

    [Fact]
    public void PostMigrationAccountAction_AllValuesExist()
    {
        PostMigrationAccountAction.None.Should().BeDefined();
        PostMigrationAccountAction.Disable.Should().BeDefined();
        PostMigrationAccountAction.Delete.Should().BeDefined();
    }

    #endregion

    #region ParseWmiDomainInfo

    [Fact]
    public void ParseWmiDomainInfo_AdJoined_ReturnsTrueAndDomain()
    {
        var json = """{"PartOfDomain": true, "Domain": "corp.local", "Workgroup": "WORKGROUP"}""";

        var (partOfDomain, domain, workgroup) = DomainService.ParseWmiDomainInfo(json);

        partOfDomain.Should().BeTrue();
        domain.Should().Be("corp.local");
        workgroup.Should().Be("WORKGROUP");
    }

    [Fact]
    public void ParseWmiDomainInfo_Workgroup_ReturnsFalse()
    {
        var json = """{"PartOfDomain": false, "Domain": null, "Workgroup": "MYGROUP"}""";

        var (partOfDomain, domain, workgroup) = DomainService.ParseWmiDomainInfo(json);

        partOfDomain.Should().BeFalse();
        workgroup.Should().Be("MYGROUP");
    }

    [Fact]
    public void ParseWmiDomainInfo_InvalidJson_ReturnsDefaults()
    {
        var (partOfDomain, domain, workgroup) = DomainService.ParseWmiDomainInfo("not json");

        partOfDomain.Should().BeFalse();
        domain.Should().BeEmpty();
        workgroup.Should().BeEmpty();
    }

    #endregion

    #region ParseDsregcmd

    [Fact]
    public void ParseDsregcmd_AzureAdJoined_ReturnsTrueAndTenantInfo()
    {
        var output = """
            +----------------------------------------------------------------------+
            | Device State                                                         |
            +----------------------------------------------------------------------+

                         AzureAdJoined : YES
                      EnterpriseJoined : NO
                          DomainJoined : NO

            +----------------------------------------------------------------------+
            | Tenant Details                                                       |
            +----------------------------------------------------------------------+

                TenantName : Contoso
                TenantId : 12345678-abcd-1234-abcd-123456789abc
                DeviceId : abcdef01-2345-6789-abcd-ef0123456789
            """;

        var (azureJoined, tenantName, tenantId, deviceId) = DomainService.ParseDsregcmd(output);

        azureJoined.Should().BeTrue();
        tenantName.Should().Be("Contoso");
        tenantId.Should().Be("12345678-abcd-1234-abcd-123456789abc");
        deviceId.Should().Be("abcdef01-2345-6789-abcd-ef0123456789");
    }

    [Fact]
    public void ParseDsregcmd_NotJoined_ReturnsFalse()
    {
        var output = """
                AzureAdJoined : NO
                EnterpriseJoined : NO
                DomainJoined : NO
            """;

        var (azureJoined, tenantName, tenantId, deviceId) = DomainService.ParseDsregcmd(output);

        azureJoined.Should().BeFalse();
        tenantName.Should().BeNull();
    }

    [Fact]
    public void ParseDsregcmd_EmptyOutput_ReturnsFalse()
    {
        var (azureJoined, _, _, _) = DomainService.ParseDsregcmd("");

        azureJoined.Should().BeFalse();
    }

    [Fact]
    public void ParseDsregcmd_JoinedWithAllFields_ParsesAll()
    {
        var output = "AzureAdJoined : YES\nTenantName : MyOrg\nTenantId : tid-123\nDeviceId : did-456";

        var (azureJoined, tenantName, tenantId, deviceId) = DomainService.ParseDsregcmd(output);

        azureJoined.Should().BeTrue();
        tenantName.Should().Be("MyOrg");
        tenantId.Should().Be("tid-123");
        deviceId.Should().Be("did-456");
    }

    #endregion

    #region ParseNltest

    [Fact]
    public void ParseNltest_ValidOutput_ReturnsDcName()
    {
        var output = """
            DC: \\DC01.corp.local
            Address: \\10.0.0.1
            Dom Guid: 12345678-abcd-1234-abcd-123456789abc
            Dom Name: corp.local
            Forest Name: corp.local
            The command completed successfully
            """;

        var dc = DomainService.ParseNltest(output);

        dc.Should().Be("DC01.corp.local");
    }

    [Fact]
    public void ParseNltest_NoOutput_ReturnsNull()
    {
        var dc = DomainService.ParseNltest("");

        dc.Should().BeNull();
    }

    #endregion

    #region ParseNtAccount

    [Fact]
    public void ParseNtAccount_LocalAccount_ReturnsLocal()
    {
        var result = DomainService.ParseNtAccount(@"MYPC\Bill", "MYPC");

        result.Should().Be(UserAccountType.Local);
    }

    [Fact]
    public void ParseNtAccount_DomainAccount_ReturnsAd()
    {
        var result = DomainService.ParseNtAccount(@"CORP\jdoe", "MYPC");

        result.Should().Be(UserAccountType.ActiveDirectory);
    }

    [Fact]
    public void ParseNtAccount_AzureAdAccount_ReturnsAzureAd()
    {
        var result = DomainService.ParseNtAccount(@"AzureAD\user@contoso.com", "MYPC");

        result.Should().Be(UserAccountType.AzureAd);
    }

    [Fact]
    public void ParseNtAccount_MicrosoftAccount_ReturnsMsa()
    {
        var result = DomainService.ParseNtAccount(@"MicrosoftAccount\user@live.com", "MYPC");

        result.Should().Be(UserAccountType.MicrosoftAccount);
    }

    #endregion

    #region GetDomainInfoAsync

    [Fact]
    public async Task GetDomainInfoAsync_AdJoined_ReturnsActiveDirectory()
    {
        var wmiJson = """{"PartOfDomain": true, "Domain": "corp.local", "Workgroup": "WORKGROUP"}""";
        _processRunner.RunAsync("powershell", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0, StandardOutput = wmiJson });

        var dsregOutput = "AzureAdJoined : NO";
        _processRunner.RunAsync("dsregcmd", "/status", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0, StandardOutput = dsregOutput });

        var nltestOutput = "DC: \\\\DC01.corp.local\nThe command completed successfully";
        _processRunner.RunAsync("nltest", Arg.Is<string>(s => s.Contains("/dsgetdc")), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0, StandardOutput = nltestOutput });

        var info = await _service.GetDomainInfoAsync();

        info.JoinType.Should().Be(DomainJoinType.ActiveDirectory);
        info.DomainOrWorkgroup.Should().Be("corp.local");
        info.IsDomainJoined.Should().BeTrue();
        info.DomainController.Should().Be("DC01.corp.local");
    }

    [Fact]
    public async Task GetDomainInfoAsync_AzureAdJoined_ReturnsAzureAd()
    {
        var wmiJson = """{"PartOfDomain": false, "Domain": null, "Workgroup": "WORKGROUP"}""";
        _processRunner.RunAsync("powershell", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0, StandardOutput = wmiJson });

        var dsregOutput = "AzureAdJoined : YES\nTenantName : Contoso\nTenantId : tid-1\nDeviceId : did-1";
        _processRunner.RunAsync("dsregcmd", "/status", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0, StandardOutput = dsregOutput });

        var info = await _service.GetDomainInfoAsync();

        info.JoinType.Should().Be(DomainJoinType.AzureAd);
        info.AzureAdTenantName.Should().Be("Contoso");
        info.IsDomainJoined.Should().BeFalse();
    }

    [Fact]
    public async Task GetDomainInfoAsync_Workgroup_ReturnsWorkgroup()
    {
        var wmiJson = """{"PartOfDomain": false, "Domain": null, "Workgroup": "MYGROUP"}""";
        _processRunner.RunAsync("powershell", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0, StandardOutput = wmiJson });

        _processRunner.RunAsync("dsregcmd", "/status", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0, StandardOutput = "AzureAdJoined : NO" });

        var info = await _service.GetDomainInfoAsync();

        info.JoinType.Should().Be(DomainJoinType.Workgroup);
        info.DomainOrWorkgroup.Should().Be("MYGROUP");
        info.IsDomainJoined.Should().BeFalse();
    }

    #endregion

    #region ClassifyUserAccountAsync

    [Fact]
    public async Task ClassifyUserAccountAsync_LocalSid_ReturnsLocal()
    {
        _processRunner.RunAsync("powershell", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = $@"{Environment.MachineName}\Bill"
            });

        var result = await _service.ClassifyUserAccountAsync("S-1-5-21-123-456-789-1001");

        result.Should().Be(UserAccountType.Local);
    }

    [Fact]
    public async Task ClassifyUserAccountAsync_DomainSid_ReturnsAd()
    {
        _processRunner.RunAsync("powershell", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0, StandardOutput = @"CORP\jdoe" });

        var result = await _service.ClassifyUserAccountAsync("S-1-5-21-999-888-777-1001");

        result.Should().Be(UserAccountType.ActiveDirectory);
    }

    [Fact]
    public async Task ClassifyUserAccountAsync_Failure_ReturnsUnknown()
    {
        _processRunner.RunAsync("powershell", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = -1, StandardError = "error" });

        var result = await _service.ClassifyUserAccountAsync("S-1-5-21-123-456-789-1001");

        result.Should().Be(UserAccountType.Unknown);
    }

    #endregion

    #region GetUserDomainAsync

    [Fact]
    public async Task GetUserDomainAsync_DomainUser_ReturnsDomain()
    {
        _processRunner.RunAsync("powershell", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult { ExitCode = 0, StandardOutput = @"CORP\jdoe" });

        var result = await _service.GetUserDomainAsync("S-1-5-21-999-888-777-1001");

        result.Should().Be("CORP");
    }

    [Fact]
    public async Task GetUserDomainAsync_LocalUser_ReturnsNull()
    {
        _processRunner.RunAsync("powershell", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult
            {
                ExitCode = 0,
                StandardOutput = $@"{Environment.MachineName}\Bill"
            });

        var result = await _service.GetUserDomainAsync("S-1-5-21-123-456-789-1001");

        result.Should().BeNull();
    }

    #endregion
}
