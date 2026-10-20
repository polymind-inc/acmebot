using Xunit;

namespace Acmebot.Cli.Tests;

public sealed class CliApplicationTests
{
    [Theory]
    [InlineData("http://acmebot.example")]
    [InlineData("ftp://acmebot.example")]
    [InlineData("/relative")]
    [InlineData("https://user:password@acmebot.example")]
    public async Task RunAsync_WithConfigSetUnsafeEndpoint_DoesNotWriteConfiguration(string endpoint)
    {
        var configPath = Path.Combine(Path.GetTempPath(), $"acmebot-{Guid.NewGuid():N}.json");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await CliApplication.RunAsync(
            ["--config", configPath, "config", "set", "--endpoint", endpoint],
            output,
            error,
            TestContext.Current.CancellationToken);

        Assert.Equal(ExitCodes.Usage, exitCode);
        Assert.Contains("must be an absolute HTTPS URL", error.ToString());
        Assert.False(File.Exists(configPath));
    }

    [Fact]
    public async Task RunAsync_WithConfigSetAudienceAndExistingHttpEndpoint_DoesNotOverwriteConfiguration()
    {
        var configPath = Path.Combine(Path.GetTempPath(), $"acmebot-{Guid.NewGuid():N}.json");
        const string configuration = """{"endpoint":"http://acmebot.example","audience":"api://original"}""";
        await File.WriteAllTextAsync(configPath, configuration, TestContext.Current.CancellationToken);

        try
        {
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = await CliApplication.RunAsync(
                ["--config", configPath, "config", "set", "--audience", "api://changed"],
                output,
                error,
                TestContext.Current.CancellationToken);

            Assert.Equal(ExitCodes.Usage, exitCode);
            Assert.Equal(configuration, await File.ReadAllTextAsync(configPath, TestContext.Current.CancellationToken));
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    [Fact]
    public async Task RunAsync_WithRemovedScopeOption_ReturnsUsage()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await CliApplication.RunAsync(
            ["--scope", "api://acmebot/user_impersonation", "--endpoint", "https://acmebot.example", "certificate", "list"],
            output,
            error,
            TestContext.Current.CancellationToken);

        Assert.Equal(ExitCodes.Usage, exitCode);
        Assert.Contains("Unknown option '--scope'.", error.ToString());
    }

    [Theory]
    [InlineData("https://acmebot.example/api/operations/abc")]
    [InlineData("/api/operations/abc")]
    [InlineData("api/operations/abc")]
    public async Task RunAsync_WithOperationWaitLocation_ReturnsUsage(string operationLocation)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await CliApplication.RunAsync(
            ["--endpoint", "https://acmebot.example", "operation", "wait", operationLocation],
            output,
            error,
            TestContext.Current.CancellationToken);

        Assert.Equal(ExitCodes.Usage, exitCode);
        Assert.Contains("Operation wait accepts an operation instance ID, not an operation URL or path.", error.ToString());
    }

    [Fact]
    public async Task RunAsync_WithConfigSet_WritesConfiguration()
    {
        var configPath = Path.Combine(Path.GetTempPath(), $"acmebot-{Guid.NewGuid():N}", "config.json");

        try
        {
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = await CliApplication.RunAsync(
                [
                    "--config",
                    configPath,
                    "config",
                    "set",
                    "--endpoint",
                    "https://acmebot.example",
                    "--audience",
                    "api://acmebot"
                ],
                output,
                error,
                TestContext.Current.CancellationToken);

            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.Contains("Configuration saved.", output.ToString());

            var config = CliConfig.Load(CommandLine.Parse(["--config", configPath]));

            Assert.Equal("https://acmebot.example", config.Endpoint);
            Assert.Equal("api://acmebot", config.Audience);
        }
        finally
        {
            var directory = Path.GetDirectoryName(configPath);

            if (directory is not null && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RunAsync_WithConfigSetScopeAudience_ReturnsUsage()
    {
        var configPath = Path.Combine(Path.GetTempPath(), $"acmebot-{Guid.NewGuid():N}", "config.json");

        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await CliApplication.RunAsync(
            ["--config", configPath, "config", "set", "--audience", "api://acmebot/.default"],
            output,
            error,
            TestContext.Current.CancellationToken);

        Assert.Equal(ExitCodes.Usage, exitCode);
        Assert.Contains("Option '--audience' must be an application ID URI or endpoint origin, not a token scope.", error.ToString());
    }
}
