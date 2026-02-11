using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using ZeroInstall.Dashboard.Api;
using ZeroInstall.Dashboard.Services;

namespace ZeroInstall.Dashboard.Tests.Api;

public class ApiKeyAuthHandlerTests
{
    private static async Task<AuthenticateResult> RunHandler(
        string? apiKeyHeader, string configuredKey)
    {
        var config = new DashboardConfiguration { ApiKey = configuredKey };
        var options = Substitute.For<IOptionsMonitor<AuthenticationSchemeOptions>>();
        options.Get(Arg.Any<string>()).Returns(new AuthenticationSchemeOptions());

        var loggerFactory = NullLoggerFactory.Instance;
        var encoder = UrlEncoder.Default;

        var handler = new ApiKeyAuthHandler(options, loggerFactory, encoder, config);

        var scheme = new AuthenticationScheme("ApiKey", null, typeof(ApiKeyAuthHandler));
        var httpContext = new DefaultHttpContext();
        if (apiKeyHeader is not null)
            httpContext.Request.Headers["X-Api-Key"] = apiKeyHeader;

        await handler.InitializeAsync(scheme, httpContext);
        return await handler.AuthenticateAsync();
    }

    [Fact]
    public async Task ValidKey_ReturnsSuccess()
    {
        var result = await RunHandler("my-secret-key", "my-secret-key");
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task MissingHeader_ReturnsFail()
    {
        var result = await RunHandler(null, "my-secret-key");
        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("Missing");
    }

    [Fact]
    public async Task WrongKey_ReturnsFail()
    {
        var result = await RunHandler("wrong-key", "correct-key");
        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("Invalid");
    }

    [Fact]
    public async Task EmptyKey_ReturnsFail()
    {
        var result = await RunHandler("", "my-secret-key");
        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("Empty");
    }

    [Fact]
    public async Task NullConfigKey_ReturnsFail()
    {
        var result = await RunHandler("any-key", "");
        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("not configured");
    }

    [Fact]
    public async Task KeyIsCaseSensitive()
    {
        var result = await RunHandler("My-Key", "my-key");
        result.Succeeded.Should().BeFalse();
    }
}
